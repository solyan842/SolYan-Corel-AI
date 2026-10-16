using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using SolYan.CorelAI.Core;

namespace SolYan.CorelAI.Agent;

public sealed class OpenAiResponsesAgent
{
    private const int MaxToolRounds = 24;
    private const int MaxVisualReviews = 2;

    private const string SystemInstructions = """
You are SolYan Corel AI, an expert CorelDRAW production assistant controlling the active Windows CorelDRAW document through deterministic local tools.

Rules:
- Inspect the document and current selection before edits when page/object geometry or existing content matters.
- All geometry exposed by SolYan Corel AI tools is in millimetres, regardless of the CorelDRAW document's native unit. Never convert millimetres yourself unless a tool explicitly asks for another unit.
- A page preview may be supplied. Use it to understand visual composition, but use document/object tools as the source of truth for exact geometry, text, pages, layers and colors.
- If compatibility or a CorelDRAW-version-specific operation is uncertain, call corel_get_capabilities before mutating the document.
- Use the tools rather than pretending a CorelDRAW change happened.
- Never invent a file path. Export only to an explicit path provided by the user/host.
- Do not delete content unless the user's request requires deletion.
- Prefer exact object-model operations. Do not describe mouse clicks.
- Keep tool calls economical. If a tool fails, inspect the returned error and recover when possible.
- The host wraps the whole task in one CorelDRAW undo command group.
- During a visual verification pass, correct only visible problems related to the user's request. Do not make unrelated aesthetic changes.
- After work is complete, briefly state what was actually changed. Never claim an action succeeded if its tool result failed.
""";

    private readonly HttpClient _http;
    private readonly ToolRegistry _tools;
    private readonly string _model;

    public OpenAiResponsesAgent(HttpClient http, ToolRegistry tools, string apiKey, string model)
    {
        _http = http;
        _http.BaseAddress ??= new Uri("https://api.openai.com/");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _tools = tools;
        _model = model;
    }

    public Task<string> RunAsync(string userPrompt, CancellationToken cancellationToken = default) =>
        RunAsync(userPrompt, previewProvider: null, cancellationToken);

    public async Task<string> RunAsync(
        string userPrompt,
        Func<CancellationToken, Task<byte[]?>>? previewProvider,
        CancellationToken cancellationToken = default)
    {
        string? previousResponseId = null;
        var initialPreview = previewProvider is null
            ? null
            : await TryCapturePreviewAsync(previewProvider, cancellationToken).ConfigureAwait(false);
        JsonNode input = BuildUserInput(userPrompt, initialPreview);
        var mutationsSinceVisualReview = false;
        var visualReviewCount = 0;

        for (var round = 0; round < MaxToolRounds; round++)
        {
            var request = new JsonObject
            {
                ["model"] = _model,
                ["instructions"] = SystemInstructions,
                ["input"] = input.DeepClone(),
                ["tools"] = BuildTools()
            };
            if (previousResponseId is not null) request["previous_response_id"] = previousResponseId;

            using var response = await _http.PostAsync(
                "v1/responses",
                new StringContent(request.ToJsonString(), Encoding.UTF8, "application/json"),
                cancellationToken).ConfigureAwait(false);

            var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"OpenAI API {(int)response.StatusCode}: {raw}");

            var root = JsonNode.Parse(raw)?.AsObject() ?? throw new InvalidOperationException("OpenAI returned invalid JSON.");
            previousResponseId = root["id"]?.GetValue<string>() ?? throw new InvalidOperationException("OpenAI response has no id.");
            var output = root["output"]?.AsArray() ?? new JsonArray();
            var calls = output.Where(n => n?["type"]?.GetValue<string>() == "function_call").ToArray();

            if (calls.Length == 0)
            {
                if (previewProvider is not null &&
                    mutationsSinceVisualReview &&
                    visualReviewCount < MaxVisualReviews)
                {
                    var reviewPreview = await TryCapturePreviewAsync(previewProvider, cancellationToken).ConfigureAwait(false);
                    if (reviewPreview is not null)
                    {
                        visualReviewCount++;
                        mutationsSinceVisualReview = false;
                        input = BuildVisualReviewInput(reviewPreview, visualReviewCount);
                        continue;
                    }
                }

                return ExtractOutputText(output) ?? "Completed.";
            }

            var toolOutputs = new JsonArray();
            foreach (var callNode in calls)
            {
                var call = callNode!.AsObject();
                var name = call["name"]?.GetValue<string>() ?? throw new InvalidOperationException("Function call has no name.");
                var callId = call["call_id"]?.GetValue<string>() ?? throw new InvalidOperationException("Function call has no call_id.");
                var argsText = call["arguments"]?.GetValue<string>() ?? "{}";
                var args = JsonNode.Parse(argsText)?.AsObject() ?? new JsonObject();
                var result = await _tools.ExecuteAsync(name, args, cancellationToken).ConfigureAwait(false);

                if (result.Success && IsVisualMutation(name))
                    mutationsSinceVisualReview = true;

                toolOutputs.Add(new JsonObject
                {
                    ["type"] = "function_call_output",
                    ["call_id"] = callId,
                    ["output"] = result.ToJson()
                });
            }

            input = toolOutputs;
        }

        throw new InvalidOperationException($"Agent exceeded {MaxToolRounds} tool rounds.");
    }

    private JsonArray BuildTools()
    {
        var result = new JsonArray();
        foreach (var tool in _tools.Tools)
        {
            result.Add(new JsonObject
            {
                ["type"] = "function",
                ["name"] = tool.Definition.Name,
                ["description"] = tool.Definition.Description,
                ["parameters"] = tool.Definition.Parameters.DeepClone(),
                ["strict"] = tool.Definition.Strict
            });
        }
        return result;
    }

    private static JsonNode BuildUserInput(string userPrompt, byte[]? previewPng)
    {
        if (previewPng is null || previewPng.Length == 0)
            return JsonValue.Create(userPrompt)!;

        return BuildImageMessage(
            userPrompt + "\n\nA PNG preview of the active CorelDRAW page is attached as visual context.",
            previewPng);
    }

    private static JsonNode BuildVisualReviewInput(byte[] previewPng, int reviewNumber) =>
        BuildImageMessage(
            $"Visual verification pass {reviewNumber}. The attached PNG shows the active CorelDRAW page after your tool actions. " +
            "Compare it with the user's original request. If something related to that request is visibly wrong or incomplete, " +
            "use the available Corel tools to correct it. If it is correct, finish with a concise completion message.",
            previewPng);

    private static JsonArray BuildImageMessage(string text, byte[] previewPng)
    {
        var content = new JsonArray
        {
            new JsonObject
            {
                ["type"] = "input_text",
                ["text"] = text
            },
            new JsonObject
            {
                ["type"] = "input_image",
                ["image_url"] = "data:image/png;base64," + Convert.ToBase64String(previewPng),
                ["detail"] = "high"
            }
        };

        return new JsonArray
        {
            new JsonObject
            {
                ["role"] = "user",
                ["content"] = content
            }
        };
    }

    private static async Task<byte[]?> TryCapturePreviewAsync(
        Func<CancellationToken, Task<byte[]?>> previewProvider,
        CancellationToken cancellationToken)
    {
        try
        {
            return await previewProvider(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Visual feedback is an enhancement. Object-model automation must remain usable if preview export is unavailable.
            return null;
        }
    }

    private static bool IsVisualMutation(string toolName)
    {
        if (toolName.StartsWith("corel_get_", StringComparison.Ordinal)) return false;
        return toolName is not "corel_select_all"
            and not "corel_activate_page"
            and not "corel_activate_layer"
            and not "corel_save"
            and not "corel_save_as"
            and not "corel_publish_pdf";
    }

    private static string? ExtractOutputText(JsonArray output)
    {
        var parts = new List<string>();
        foreach (var itemNode in output)
        {
            if (itemNode is not JsonObject item || item["type"]?.GetValue<string>() != "message") continue;
            if (item["content"] is not JsonArray content) continue;
            foreach (var contentNode in content)
            {
                if (contentNode is JsonObject part && part["type"]?.GetValue<string>() == "output_text")
                {
                    var text = part["text"]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(text)) parts.Add(text);
                }
            }
        }
        return parts.Count == 0 ? null : string.Join(Environment.NewLine, parts);
    }
}
