using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using SolYan.CorelAI.Core;

namespace SolYan.CorelAI.Agent;

public sealed class OpenAiResponsesAgent
{
    private const int MaxToolRounds = 20;

    private const string SystemInstructions = """
You are SolYan Corel AI, an expert CorelDRAW production assistant controlling the active Windows CorelDRAW document through deterministic local tools.

Rules:
- Inspect the document and current selection before edits when page/object geometry or existing content matters.
- Use the tools rather than pretending a CorelDRAW change happened.
- Never invent a file path. Export only to an explicit path provided by the user/host.
- Do not delete content unless the user's request requires deletion.
- Prefer exact object-model operations. Do not describe mouse clicks.
- Keep tool calls economical. If a tool fails, inspect the returned error and recover when possible.
- The host wraps the whole task in one CorelDRAW undo command group.
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

    public async Task<string> RunAsync(string userPrompt, CancellationToken cancellationToken = default)
    {
        string? previousResponseId = null;
        JsonNode input = JsonValue.Create(userPrompt)!;

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
                return ExtractOutputText(output) ?? "Completed.";

            var toolOutputs = new JsonArray();
            foreach (var callNode in calls)
            {
                var call = callNode!.AsObject();
                var name = call["name"]?.GetValue<string>() ?? throw new InvalidOperationException("Function call has no name.");
                var callId = call["call_id"]?.GetValue<string>() ?? throw new InvalidOperationException("Function call has no call_id.");
                var argsText = call["arguments"]?.GetValue<string>() ?? "{}";
                var args = JsonNode.Parse(argsText)?.AsObject() ?? new JsonObject();
                var result = await _tools.ExecuteAsync(name, args, cancellationToken).ConfigureAwait(false);

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
