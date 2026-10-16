using System.Text.Json.Nodes;
using SolYan.CorelAI.Agent;
using SolYan.CorelAI.Core;
using SolYan.CorelAI.Corel;

namespace SolYan.CorelAI.Host;

public sealed class CorelAiRuntime
{
    private readonly CorelSession _corel;
    private readonly HttpClient _http;
    private readonly ToolRegistry _tools;
    private string _model;
    private string? _apiKey;
    private OpenAiResponsesAgent? _agent;

    public CorelAiRuntime(
        CorelSession corel,
        HttpClient http,
        ToolRegistry tools,
        string model,
        string? initialApiKey)
    {
        _corel = corel;
        _http = http;
        _tools = tools;
        _model = WindowsSettingsStore.NormalizeModel(string.IsNullOrWhiteSpace(model) ? "gpt-6.1-sol" : model);

        if (!string.IsNullOrWhiteSpace(initialApiKey))
            ConfigureApiKey(initialApiKey, persist: false);
    }

    public bool AiConfigured => _agent is not null;
    public string Model => _model;

    public void ConfigureApiKey(string apiKey, bool persist = true)
    {
        var key = apiKey?.Trim();
        if (string.IsNullOrWhiteSpace(key) || key.Length < 20)
            throw new ArgumentException("OpenAI API key is empty or invalid.", nameof(apiKey));

        if (persist) WindowsSecretStore.SaveApiKey(key);
        _apiKey = key;
        RebuildAgent();
    }

    public void ClearApiKey()
    {
        WindowsSecretStore.ClearApiKey();
        _apiKey = null;
        _agent = null;
        _http.DefaultRequestHeaders.Authorization = null;
    }

    public void ConfigureModel(string model, bool persist = true)
    {
        var normalized = WindowsSettingsStore.NormalizeModel(model);
        if (persist) WindowsSettingsStore.SaveModel(normalized);
        _model = normalized;
        RebuildAgent();
    }

    public Task<JsonObject> GetCapabilitiesAsync(CancellationToken cancellationToken = default) =>
        _corel.GetCapabilitiesAsync(cancellationToken);

    public async Task<JsonObject> GetContextAsync(CancellationToken cancellationToken = default)
    {
        var document = await _corel.GetDocumentStateAsync(cancellationToken).ConfigureAwait(false);
        var selection = document["open"]?.GetValue<bool>() == true
            ? await _corel.GetSelectionAsync(cancellationToken).ConfigureAwait(false)
            : new JsonArray();

        return new JsonObject
        {
            ["ai_configured"] = AiConfigured,
            ["model"] = _model,
            ["visual_feedback"] = true,
            ["document"] = document,
            ["selection"] = selection
        };
    }

    public Task UndoAsync(CancellationToken cancellationToken = default) =>
        _corel.UndoAsync(cancellationToken);

    public async Task<string> RunTaskAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var agent = _agent ?? throw new InvalidOperationException(
            "OpenAI API key is not configured. Enter it in the SolYan AI Docker settings.");
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Task cannot be empty.", nameof(prompt));

        var grouped = await _corel.BeginAiJobAsync("SolYan AI", cancellationToken).ConfigureAwait(false);
        var groupClosed = false;
        try
        {
            return await agent.RunAsync(
                prompt,
                async ct => await _corel.RenderPagePreviewPngAsync(110, ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (grouped)
            {
                try
                {
                    await _corel.EndAiJobAsync(cancellationToken).ConfigureAwait(false);
                    groupClosed = true;
                    await _corel.UndoAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // Preserve the original exception. The next action can report rollback trouble if Corel rejected it.
                }
            }
            throw;
        }
        finally
        {
            if (grouped && !groupClosed)
                await _corel.EndAiJobAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void RebuildAgent()
    {
        _agent = string.IsNullOrWhiteSpace(_apiKey)
            ? null
            : new OpenAiResponsesAgent(_http, _tools, _apiKey, _model);
    }
}
