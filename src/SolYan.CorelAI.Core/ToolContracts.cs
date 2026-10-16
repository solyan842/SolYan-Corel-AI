using System.Text.Json.Nodes;

namespace SolYan.CorelAI.Core;

public sealed record ToolDefinition(
    string Name,
    string Description,
    JsonObject Parameters,
    bool Strict = true);

public sealed record ToolExecutionResult(bool Success, JsonNode? Data = null, string? Error = null)
{
    public string ToJson() => new JsonObject
    {
        ["success"] = Success,
        ["data"] = Data?.DeepClone(),
        ["error"] = Error
    }.ToJsonString();

    public static ToolExecutionResult Ok(JsonNode? data = null) => new(true, data);
    public static ToolExecutionResult Fail(string error) => new(false, null, error);
}

public interface IAiTool
{
    ToolDefinition Definition { get; }
    ValueTask<ToolExecutionResult> ExecuteAsync(JsonObject arguments, CancellationToken cancellationToken = default);
}

public sealed class ToolRegistry
{
    private readonly Dictionary<string, IAiTool> _tools;

    public ToolRegistry(IEnumerable<IAiTool> tools)
    {
        _tools = tools.ToDictionary(t => t.Definition.Name, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<IAiTool> Tools => _tools.Values;

    public async ValueTask<ToolExecutionResult> ExecuteAsync(
        string name,
        JsonObject arguments,
        CancellationToken cancellationToken = default)
    {
        if (!_tools.TryGetValue(name, out var tool))
            return ToolExecutionResult.Fail($"Unknown tool: {name}");

        try
        {
            return await tool.ExecuteAsync(arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return ToolExecutionResult.Fail($"{ex.GetType().Name}: {ex.Message}");
        }
    }
}
