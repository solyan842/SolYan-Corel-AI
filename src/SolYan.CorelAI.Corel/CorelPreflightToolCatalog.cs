using System.Text.Json.Nodes;
using SolYan.CorelAI.Core;

namespace SolYan.CorelAI.Corel;

public static class CorelPreflightToolCatalog
{
    public static IReadOnlyCollection<IAiTool> Create(CorelSession corel) =>
    [
        Tool(
            "corel_preflight_document",
            "Inspect the entire active CorelDRAW document for production risks without modifying it. Reports missing fonts, text overflow, low-resolution bitmaps, RGB fill/outline usage, locked shapes, hidden/non-editable layers, font usage and color-type summaries.",
            Obj(
                ("min_bitmap_dpi", Integer(36, 1200)),
                ("flag_rgb", Bool()),
                ("check_fonts", Bool())),
            async (arguments, ct) => ToolExecutionResult.Ok(await corel.RunPreflightAsync(
                I(arguments, "min_bitmap_dpi"),
                B(arguments, "flag_rgb"),
                B(arguments, "check_fonts"),
                ct)))
    ];

    private static IAiTool Tool(
        string name,
        string description,
        JsonObject parameters,
        Func<JsonObject, CancellationToken, ValueTask<ToolExecutionResult>> execute) =>
        new DelegateTool(new ToolDefinition(name, description, parameters), execute);

    private static JsonObject Integer(int minimum, int maximum) => new()
    {
        ["type"] = "integer",
        ["minimum"] = minimum,
        ["maximum"] = maximum
    };

    private static JsonObject Bool() => new() { ["type"] = "boolean" };

    private static JsonObject Obj(params (string Name, JsonObject Schema)[] fields)
    {
        var properties = new JsonObject();
        var required = new JsonArray();
        foreach (var (name, schema) in fields)
        {
            properties[name] = schema;
            required.Add(name);
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false
        };
    }

    private static int I(JsonObject arguments, string key) =>
        arguments[key]?.GetValue<int>() ?? throw new ArgumentException($"Missing {key}");

    private static bool B(JsonObject arguments, string key) =>
        arguments[key]?.GetValue<bool>() ?? throw new ArgumentException($"Missing {key}");

    private sealed class DelegateTool(
        ToolDefinition definition,
        Func<JsonObject, CancellationToken, ValueTask<ToolExecutionResult>> execute) : IAiTool
    {
        public ToolDefinition Definition { get; } = definition;

        public ValueTask<ToolExecutionResult> ExecuteAsync(
            JsonObject arguments,
            CancellationToken cancellationToken = default) =>
            execute(arguments, cancellationToken);
    }
}
