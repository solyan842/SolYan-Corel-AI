using System.Text.Json.Nodes;
using SolYan.CorelAI.Agent;
using SolYan.CorelAI.Corel;

namespace SolYan.CorelAI.Host;

public sealed class CorelAiRuntime
{
    private readonly CorelSession _corel;
    private readonly OpenAiResponsesAgent? _agent;

    public CorelAiRuntime(CorelSession corel, OpenAiResponsesAgent? agent)
    {
        _corel = corel;
        _agent = agent;
    }

    public bool AiConfigured => _agent is not null;

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
            ["document"] = document,
            ["selection"] = selection
        };
    }

    public Task UndoAsync(CancellationToken cancellationToken = default) =>
        _corel.UndoAsync(cancellationToken);

    public async Task<string> RunTaskAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (_agent is null)
            throw new InvalidOperationException("OPENAI_API_KEY is not configured. Set it for the engine process before running AI tasks.");
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Task cannot be empty.", nameof(prompt));

        var grouped = await _corel.BeginAiJobAsync("SolYan AI", cancellationToken).ConfigureAwait(false);
        var groupClosed = false;
        try
        {
            return await _agent.RunAsync(prompt, cancellationToken).ConfigureAwait(false);
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
                    // Preserve the original exception. The host will report rollback trouble separately on the next action.
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
}
