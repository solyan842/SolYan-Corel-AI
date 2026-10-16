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
        Tool("corel_get_pages_layers", "Inspect all pages and their layers, including active, visibility and editability state.", Empty(),
            async (_, ct) => ToolExecutionResult.Ok(await corel.GetPagesAndLayersAsync(ct))),
        Tool("corel_get_selection", "Inspect currently selected objects, geometry and text when available. All returned geometry is in millimetres.", Empty(),
            async (_, ct) => ToolExecutionResult.Ok(await corel.GetSelectionAsync(ct))),

        Tool("corel_select_all", "Select all shapes on the active page.", Empty(),
            async (_, ct) => { await corel.SelectAllAsync(ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_move_selection", "Move the current selection by an exact delta in millimetres without changing the document's unit setting.", Obj(("dx_mm", Number()), ("dy_mm", Number())),
            async (a, ct) => { await corel.MoveSelectionAsync(D(a,"dx_mm"), D(a,"dy_mm"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_move_selection_to", "Move the selection so its bounding-box lower-left corner is at an exact millimetre coordinate.", Obj(("left_mm", Number()), ("bottom_mm", Number())),
            async (a, ct) => { await corel.MoveSelectionToAsync(D(a,"left_mm"), D(a,"bottom_mm"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_resize_selection", "Resize the selection using millimetres. Use 0 for one dimension only when preserve_aspect is true.", Obj(("width_mm", NonNegativeNumber()), ("height_mm", NonNegativeNumber()), ("preserve_aspect", Bool())),
            async (a, ct) => { await corel.ResizeSelectionAsync(D(a,"width_mm"), D(a,"height_mm"), B(a,"preserve_aspect"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_rotate_selection", "Rotate the current selection by an angle in degrees.", Obj(("angle_degrees", Number())),
            async (a, ct) => { await corel.RotateSelectionAsync(D(a,"angle_degrees"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_center_selection_on_page", "Center the current selection geometrically on the active page, respecting the document drawing origin.", Empty(),
            async (_, ct) => { await corel.CenterSelectionOnPageAsync(ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_align_selection", "Align every selected object to the active page or to the current selection bounding box.", Obj(("mode", Enum("left","center_x","right","bottom","center_y","top")), ("target", Enum("page","selection"))),
            async (a, ct) => { await corel.AlignSelectionAsync(S(a,"mode"), S(a,"target"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_distribute_selection", "Distribute three or more selected objects with equal gaps while preserving the two outside bounds.", Obj(("axis", Enum("horizontal","vertical"))),
            async (a, ct) => { await corel.DistributeSelectionAsync(S(a,"axis"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_order_selection", "Send selected objects to the front or back of their layer.", Obj(("order", Enum("front","back"))),
            async (a, ct) => { await corel.OrderSelectionAsync(S(a,"order"), ct); return ToolExecutionResult.Ok(); }),

        Tool("corel_set_fill_rgb", "Apply a uniform RGB fill to every selected shape.", Obj(("r", Byte()), ("g", Byte()), ("b", Byte())),
            async (a, ct) => { await corel.SetFillRgbAsync(I(a,"r"), I(a,"g"), I(a,"b"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_set_outline_rgb", "Set selected shapes' outline RGB color and width in millimetres.", Obj(("r", Byte()), ("g", Byte()), ("b", Byte()), ("width_mm", NonNegativeNumber())),
            async (a, ct) => { await corel.SetOutlineRgbAsync(I(a,"r"), I(a,"g"), I(a,"b"), D(a,"width_mm"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_set_fill_cmyk", "Apply a uniform CMYK fill to every selected shape. CMYK channels are percentages from 0 to 100.", Obj(("c", Percent()), ("m", Percent()), ("y", Percent()), ("k", Percent())),
            async (a, ct) => { await corel.SetFillCmykAsync(I(a,"c"), I(a,"m"), I(a,"y"), I(a,"k"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_set_outline_cmyk", "Set selected shapes' outline CMYK color and width in millimetres.", Obj(("c", Percent()), ("m", Percent()), ("y", Percent()), ("k", Percent()), ("width_mm", NonNegativeNumber())),
            async (a, ct) => { await corel.SetOutlineCmykAsync(I(a,"c"), I(a,"m"), I(a,"y"), I(a,"k"), D(a,"width_mm"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_set_selected_text", "Replace the text story of editable text objects in the current selection.", Obj(("text", String())),
            async (a, ct) => { await corel.SetSelectedTextAsync(S(a,"text"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_set_selected_typography", "Set font family, point size, bold and italic for editable text shapes in the current selection.", Obj(("font", String()), ("size_pt", PositiveNumber()), ("bold", Bool()), ("italic", Bool())),
            async (a, ct) => { await corel.SetSelectedTypographyAsync(S(a,"font"), D(a,"size_pt"), B(a,"bold"), B(a,"italic"), ct); return ToolExecutionResult.Ok(); }),

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

        Tool("corel_activate_page", "Activate a page by its 1-based page index.", Obj(("page_index", PositiveInteger())),
            async (a, ct) => { await corel.ActivatePageAsync(I(a,"page_index"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_create_layer", "Create a layer on the active page and optionally activate it.", Obj(("name", String()), ("activate", Bool())),
            async (a, ct) => { await corel.CreateLayerAsync(S(a,"name"), B(a,"activate"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_activate_layer", "Activate a layer by name on the active page.", Obj(("name", String())),
            async (a, ct) => { await corel.ActivateLayerAsync(S(a,"name"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_set_layer_state", "Set both visibility and editability of a named layer on the active page.", Obj(("name", String()), ("visible", Bool()), ("editable", Bool())),
            async (a, ct) => { await corel.SetLayerStateAsync(S(a,"name"), B(a,"visible"), B(a,"editable"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_import_file", "Import a supported local file into the active layer. The path must already exist; imported objects become selected.", Obj(("path", String())),
            async (a, ct) => { await corel.ImportFileAsync(S(a,"path"), ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_save", "Save the active document to its existing file. Fails for a never-saved document rather than opening a modal Save As dialog.", Empty(),
            async (_, ct) => { await corel.SaveAsync(ct); return ToolExecutionResult.Ok(); }),
        Tool("corel_save_as", "Save the active document to an explicit local path. The target directory must already exist.", Obj(("path", String())),
            async (a, ct) => { await corel.SaveAsAsync(S(a,"path"), ct); return ToolExecutionResult.Ok(); }),
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
    private static JsonObject Percent() => new() { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 100 };
    private static JsonObject PositiveInteger() => new() { ["type"] = "integer", ["minimum"] = 1 };
    private static JsonObject Bool() => new() { ["type"] = "boolean" };
    private static JsonObject String() => new() { ["type"] = "string" };
    private static JsonObject Enum(params string[] values) => new()
    {
        ["type"] = "string",
        ["enum"] = new JsonArray(values.Select(JsonValue.Create).ToArray())
    };

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
