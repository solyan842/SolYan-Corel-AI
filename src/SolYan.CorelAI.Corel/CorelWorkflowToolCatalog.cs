using System.Text.Json.Nodes;
using SolYan.CorelAI.Core;

namespace SolYan.CorelAI.Corel;

public static class CorelWorkflowToolCatalog
{
    public static IReadOnlyCollection<IAiTool> Create(CorelSession corel) =>
    [
        Tool(
            "corel_fit_selection_to_page",
            "Fit the current selection into the active page using an exact millimetre margin, then align it horizontally and vertically. This deterministic compound workflow returns the final selection bounding box in millimetres.",
            Obj(
                ("margin_mm", NonNegativeNumber()),
                ("preserve_aspect", Bool()),
                ("horizontal_align", Enum("left", "center", "right")),
                ("vertical_align", Enum("bottom", "center", "top"))),
            async (a, ct) => ToolExecutionResult.Ok(await corel.FitSelectionToPageAsync(
                D(a, "margin_mm"),
                B(a, "preserve_aspect"),
                S(a, "horizontal_align"),
                S(a, "vertical_align"),
                ct))),

        Tool(
            "corel_arrange_selection_grid",
            "Arrange two or more currently selected objects into a deterministic top-to-bottom, left-to-right grid without resizing them. Gaps are exact millimetres and the final layout geometry is returned.",
            Obj(
                ("columns", Integer(1, 50)),
                ("horizontal_gap_mm", NonNegativeNumber()),
                ("vertical_gap_mm", NonNegativeNumber())),
            async (a, ct) => ToolExecutionResult.Ok(await corel.ArrangeSelectionGridAsync(
                I(a, "columns"),
                D(a, "horizontal_gap_mm"),
                D(a, "vertical_gap_mm"),
                ct)))
    ];

    private static IAiTool Tool(
        string name,
        string description,
        JsonObject parameters,
        Func<JsonObject, CancellationToken, ValueTask<ToolExecutionResult>> execute) =>
        new DelegateTool(new ToolDefinition(name, description, parameters), execute);

    private static JsonObject NonNegativeNumber() => new()
    {
        ["type"] = "number",
        ["minimum"] = 0
    };

    private static JsonObject Integer(int minimum, int maximum) => new()
    {
        ["type"] = "integer",
        ["minimum"] = minimum,
        ["maximum"] = maximum
    };

    private static JsonObject Bool() => new() { ["type"] = "boolean" };

    private static JsonObject Enum(params string[] values)
    {
        var items = new JsonArray();
        foreach (var value in values) items.Add(value);
        return new JsonObject { ["type"] = "string", ["enum"] = items };
    }

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

    private static double D(JsonObject arguments, string key) =>
        arguments[key]?.GetValue<double>() ?? throw new ArgumentException($"Missing {key}");

    private static int I(JsonObject arguments, string key) =>
        arguments[key]?.GetValue<int>() ?? throw new ArgumentException($"Missing {key}");

    private static bool B(JsonObject arguments, string key) =>
        arguments[key]?.GetValue<bool>() ?? throw new ArgumentException($"Missing {key}");

    private static string S(JsonObject arguments, string key) =>
        arguments[key]?.GetValue<string>() ?? throw new ArgumentException($"Missing {key}");

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
