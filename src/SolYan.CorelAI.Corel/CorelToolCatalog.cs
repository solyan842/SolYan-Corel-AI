using System.Text.Json.Nodes;
using SolYan.CorelAI.Core;

namespace SolYan.CorelAI.Corel;

public static class CorelToolCatalog
{
    public static IReadOnlyCollection<IAiTool> Create(CorelSession corel) =>
    [
        Tool("corel_get_capabilities", "Inspect the connected CorelDRAW version, active document unit, and runtime automation capabilities. Call this when compatibility is uncertain.", Empty(),
            async (_, ct) => ToolExecutionResult.Ok(await corel.GetCapabilitiesAsync(ct))),
        Tool("corel_get_document", "Inspect the active CorelDRAW document and page. All returned geometry is in millimetres.", Empty(),
            async (_, ct) => ToolExecutionResult.Ok(await corel.GetDocumentStateAsync(ct))),
        Tool("corel_get_selection", "Inspect currently selected objects, geometry and text when available. All returned geometry is in millimetres.", Empty(),
            async (_, ct) => ToolExecutionResult.Ok(await corel.GetSelectionAsync(ct))),
        Tool("corel_select_all", "Select all shapes on the active page.", Empty(),
            async (_, ct) => { await corel.SelectAllAsync(ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_move_selection", "Move the current selection by an exact delta in millimetres without changing the document's unit setting.", Obj(("dx_mm", Number()), ("dy_mm", Number())),
            async (a, ct) => { await corel.MoveSelectionAsync(D(a,"dx_mm"), D(a,"dy_mm"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_resize_selection", "Resize the selection using millimetres. Use 0 for one dimension only when preserve_aspect is true.", Obj(("width_mm", NonNegativeNumber()), ("height_mm", NonNegativeNumber()), ("preserve_aspect", Bool())),
            async (a, ct) => { await corel.ResizeSelectionAsync(D(a,"width_mm"), D(a,"height_mm"), B(a,"preserve_aspect"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_center_selection_on_page", "Center the current selection geometrically on the active page.", Empty(),
            async (_, ct) => { await corel.CenterSelectionOnPageAsync(ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_set_fill_rgb", "Apply a uniform RGB fill to every selected shape.", Obj(("r", Byte()), ("g", Byte()), ("b", Byte())),
            async (a, ct) => { await corel.SetFillRgbAsync(I(a,"r"), I(a,"g"), I(a,"b"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_set_outline_rgb", "Set selected shapes' outline RGB color and width in millimetres.", Obj(("r", Byte()), ("g", Byte()), ("b", Byte()), ("width_mm", NonNegativeNumber())),
            async (a, ct) => { await corel.SetOutlineRgbAsync(I(a,"r"), I(a,"g"), I(a,"b"), D(a,"width_mm"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_set_selected_text", "Replace the text story of editable text objects in the current selection.", Obj(("text", String())),
            async (a, ct) => { await corel.SetSelectedTextAsync(S(a,"text"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_create_rectangle", "Create and select a rectangle on the active layer. Position and size are in millimetres.", Obj(("x_mm", Number()), ("y_mm", Number()), ("width_mm", PositiveNumber()), ("height_mm", PositiveNumber())),
            async (a, ct) => { await corel.CreateRectangleAsync(D(a,"x_mm"), D(a,"y_mm"), D(a,"width_mm"), D(a,"height_mm"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_create_artistic_text", "Create and select artistic text on the active layer. Position is in millimetres.", Obj(("x_mm", Number()), ("y_mm", Number()), ("text", String())),
            async (a, ct) => { await corel.CreateArtisticTextAsync(D(a,"x_mm"), D(a,"y_mm"), S(a,"text"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_duplicate_selection", "Duplicate the current selection and select the duplicate.", Empty(),
            async (_, ct) => { await corel.DuplicateSelectionAsync(ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_group_selection", "Group the current selection and select the resulting group.", Empty(),
            async (_, ct) => { await corel.GroupSelectionAsync(ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_delete_selection", "Delete the current selection. Use only when the user's request clearly requires deletion; the AI job remains undoable.", Empty(),
            async (_, ct) => { await corel.DeleteSelectionAsync(ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_publish_pdf", "Publish the active CorelDRAW document to the exact PDF path supplied by the user or host.", Obj(("path", String())),
            async (a, ct) => { await corel.PublishPdfAsync(S(a,"path"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_undo", "Undo the most recent CorelDRAW action when the user explicitly asks to undo or a recovery step is required.", Empty(),
            async (_, ct) => { await corel.UndoAsync(ct); return ToolExecutionResult.Ok(); })
    ];

    private static IAiTool Tool(string name, string description, JsonObject parameters,
        Func<JsonObject, CancellationToken, ValueTask<ToolExecutionResult>> execute) =>
        new DelegateTool(new ToolDefinition(name, description, parameters), execute);

    private static JsonObject Empty() => Obj();
    private static JsonObject Number() => new() { ["type"] = "number" };
    private static JsonObject NonNegativeNumber() => new() { ["type"] = "number", ["minimum"] = 0 };
    private static JsonObject PositiveNumber() => new() { ["type"] = "number", ["exclusiveMinimum"] = 0 };
    private static JsonObject Byte() => new() { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 255 };
    private static JsonObject Bool() => new() { ["type"] = "boolean" };
    private static JsonObject String() => new() { ["type"] = "string" };

    private static JsonObject Obj(params (string Name, JsonObject Schema)[] fields)
    {
        var props = new JsonObject();
        var required = new JsonArray();
        foreach (var (name, schema) in fields)
        {
            props[name] = schema;
            required.Add(name);
        }
        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = props,
            ["required"] = required,
            ["additionalProperties"] = false
        };
    }

    private static double D(JsonObject a, string key) => a[key]?.GetValue<double>() ?? throw new ArgumentException($"Missing {key}");
    private static int I(JsonObject a, string key) => a[key]?.GetValue<int>() ?? throw new ArgumentException($"Missing {key}");
    private static bool B(JsonObject a, string key) => a[key]?.GetValue<bool>() ?? throw new ArgumentException($"Missing {key}");
    private static string S(JsonObject a, string key) => a[key]?.GetValue<string>() ?? throw new ArgumentException($"Missing {key}");

    private sealed class DelegateTool(
        ToolDefinition definition,
        Func<JsonObject, CancellationToken, ValueTask<ToolExecutionResult>> execute) : IAiTool
    {
        public ToolDefinition Definition { get; } = definition;
        public ValueTask<ToolExecutionResult> ExecuteAsync(JsonObject arguments, CancellationToken cancellationToken = default) =>
            execute(arguments, cancellationToken);
    }
}
