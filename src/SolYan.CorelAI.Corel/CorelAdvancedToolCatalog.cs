using System.Text.Json.Nodes;
using SolYan.CorelAI.Core;

namespace SolYan.CorelAI.Corel;

public static class CorelAdvancedToolCatalog
{
    public static IReadOnlyCollection<IAiTool> Create(CorelSession corel) =>
    [
        Tool("corel_list_page_shapes", "List recursively discovered shapes on the active page with stable StaticID, layer, text and millimetre geometry. Use this before object-specific operations that require unambiguous identity.", Obj(("limit", Integer(1, 2000))),
            async (a, ct) => ToolExecutionResult.Ok(await corel.ListPageShapesAsync(I(a,"limit"), ct))),
        Tool("corel_select_shapes_by_ids", "Replace the current selection with shapes identified by stable CorelDRAW StaticIDs from the active page.", Obj(("static_ids", IntegerArray(1, 500))),
            async (a, ct) => { await corel.SelectShapesByStaticIdsAsync(Ids(a,"static_ids"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_select_matching_shapes", "Select shapes by name substring, text substring and/or exact layer name. Supply an empty string for an unused criterion; at least one criterion must be non-empty.", Obj(("name_contains", String()), ("text_contains", String()), ("layer_name", String()), ("case_sensitive", Bool())),
            async (a, ct) => ToolExecutionResult.Ok(await corel.SelectMatchingShapesAsync(S(a,"name_contains"), S(a,"text_contains"), S(a,"layer_name"), B(a,"case_sensitive"), ct))),
        Tool("corel_find_replace_text", "Find and replace text in selected shapes or all shapes on the active page. Use page scope only when the user's request clearly applies broadly.", Obj(("find", String(1)), ("replacement", String()), ("case_sensitive", Bool()), ("scope", Enum("selection","page"))),
            async (a, ct) => ToolExecutionResult.Ok(await corel.FindReplaceTextAsync(S(a,"find"), S(a,"replacement"), B(a,"case_sensitive"), S(a,"scope"), ct))),

        Tool("corel_ungroup_selection", "Ungroup one level of the current selection.", Empty(),
            async (_, ct) => { await corel.UngroupSelectionAsync(ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_convert_selection_to_curves", "Convert all selected shapes to curves. This can make text no longer editable; use only when the task requires curves/outlines.", Empty(),
            async (_, ct) => { await corel.ConvertSelectionToCurvesAsync(ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_combine_selection", "Combine two or more selected shapes into one compound curve.", Empty(),
            async (_, ct) => { await corel.CombineSelectionAsync(ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_flip_selection", "Mirror the current selection horizontally, vertically, or on both axes.", Obj(("axis", Enum("horizontal","vertical","both"))),
            async (a, ct) => { await corel.FlipSelectionAsync(S(a,"axis"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_duplicate_grid", "Create a rectangular array from the current selection. The original selection occupies row 1 column 1; offsets are in millimetres and the total grid is capped at 100 cells.", Obj(("rows", Integer(1,20)), ("columns", Integer(1,20)), ("offset_x_mm", Number()), ("offset_y_mm", Number())),
            async (a, ct) => { await corel.DuplicateSelectionGridAsync(I(a,"rows"), I(a,"columns"), D(a,"offset_x_mm"), D(a,"offset_y_mm"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_move_selection_to_layer", "Move all selected shapes to a named layer on the active page.", Obj(("layer_name", String(1))),
            async (a, ct) => { await corel.MoveSelectionToLayerAsync(S(a,"layer_name"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_boolean_shapes", "Apply weld, intersect, or trim to two explicitly identified shapes. Obtain StaticIDs first so source and target are unambiguous.", Obj(("source_static_id", PositiveInteger()), ("target_static_id", PositiveInteger()), ("operation", Enum("weld","intersect","trim")), ("leave_source", Bool()), ("leave_target", Bool())),
            async (a, ct) => ToolExecutionResult.Ok(await corel.BooleanShapesByIdAsync(I(a,"source_static_id"), I(a,"target_static_id"), S(a,"operation"), B(a,"leave_source"), B(a,"leave_target"), ct))),
        Tool("corel_powerclip_add", "Put explicitly identified shapes into an explicitly identified PowerClip container. Get StaticIDs first. Set center_in_container according to the user's layout intent.", Obj(("content_static_ids", IntegerArray(1,200)), ("container_static_id", PositiveInteger()), ("center_in_container", Bool())),
            async (a, ct) => { await corel.AddShapesToPowerClipAsync(Ids(a,"content_static_ids"), I(a,"container_static_id"), B(a,"center_in_container"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_powerclip_extract", "Extract all contents from a PowerClip container identified by StaticID, leaving container and extracted contents as separate objects.", Obj(("container_static_id", PositiveInteger())),
            async (a, ct) => ToolExecutionResult.Ok(await corel.ExtractPowerClipAsync(I(a,"container_static_id"), ct))),

        Tool("corel_add_pages", "Append blank pages with an exact millimetre width and height.", Obj(("count", Integer(1,50)), ("width_mm", PositiveNumber()), ("height_mm", PositiveNumber())),
            async (a, ct) => ToolExecutionResult.Ok(await corel.AddPagesAsync(I(a,"count"), D(a,"width_mm"), D(a,"height_mm"), ct))),
        Tool("corel_rename_page", "Rename a page by its 1-based page index.", Obj(("page_index", PositiveInteger()), ("name", String(1))),
            async (a, ct) => { await corel.RenamePageAsync(I(a,"page_index"), S(a,"name"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_delete_pages", "Delete a contiguous range of pages. This is destructive and must only be used when the user's request clearly asks to delete those pages. The tool refuses to delete every page.", Obj(("start_page", PositiveInteger()), ("count", PositiveInteger())),
            async (a, ct) => { await corel.DeletePagesAsync(I(a,"start_page"), I(a,"count"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_rename_layer", "Rename a layer on the active page.", Obj(("current_name", String(1)), ("new_name", String(1))),
            async (a, ct) => { await corel.RenameLayerAsync(S(a,"current_name"), S(a,"new_name"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_delete_layer", "Delete a named layer and all shapes it contains. Use only for an explicit user deletion request; delete_contents must be true as an additional destructive-action acknowledgement.", Obj(("name", String(1)), ("delete_contents", Bool())),
            async (a, ct) => ToolExecutionResult.Ok(await corel.DeleteLayerAsync(S(a,"name"), B(a,"delete_contents"), ct)))
    ];

    private static IAiTool Tool(string name, string description, JsonObject parameters,
        Func<JsonObject, CancellationToken, ValueTask<ToolExecutionResult>> execute) =>
        new DelegateTool(new ToolDefinition(name, description, parameters), execute);

    private static JsonObject Empty() => Obj();
    private static JsonObject Number() => new() { ["type"] = "number" };
    private static JsonObject PositiveNumber() => new() { ["type"] = "number", ["exclusiveMinimum"] = 0 };
    private static JsonObject PositiveInteger() => Integer(1, int.MaxValue);
    private static JsonObject Integer(int minimum, int maximum) => new() { ["type"] = "integer", ["minimum"] = minimum, ["maximum"] = maximum };
    private static JsonObject Bool() => new() { ["type"] = "boolean" };
    private static JsonObject String(int minimumLength = 0) => new() { ["type"] = "string", ["minLength"] = minimumLength };

    private static JsonObject IntegerArray(int minimumItems, int maximumItems) => new()
    {
        ["type"] = "array",
        ["minItems"] = minimumItems,
        ["maxItems"] = maximumItems,
        ["items"] = PositiveInteger()
    };

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

    private static double D(JsonObject arguments, string key) => arguments[key]?.GetValue<double>() ?? throw new ArgumentException($"Missing {key}");
    private static int I(JsonObject arguments, string key) => arguments[key]?.GetValue<int>() ?? throw new ArgumentException($"Missing {key}");
    private static bool B(JsonObject arguments, string key) => arguments[key]?.GetValue<bool>() ?? throw new ArgumentException($"Missing {key}");
    private static string S(JsonObject arguments, string key) => arguments[key]?.GetValue<string>() ?? throw new ArgumentException($"Missing {key}");
    private static IReadOnlyCollection<int> Ids(JsonObject arguments, string key) =>
        arguments[key]?.AsArray().Select(node => node?.GetValue<int>() ?? throw new ArgumentException($"Invalid {key}"))
            .ToArray() ?? throw new ArgumentException($"Missing {key}");

    private sealed class DelegateTool(
        ToolDefinition definition,
        Func<JsonObject, CancellationToken, ValueTask<ToolExecutionResult>> execute) : IAiTool
    {
        public ToolDefinition Definition { get; } = definition;
        public ValueTask<ToolExecutionResult> ExecuteAsync(JsonObject arguments, CancellationToken cancellationToken = default) =>
            execute(arguments, cancellationToken);
    }
}
