using System.Text.Json.Nodes;
using SolYan.CorelAI.Core;

namespace SolYan.CorelAI.Corel;

public static class CorelExportToolCatalog
{
    public static IReadOnlyCollection<IAiTool> Create(CorelSession corel) =>
    [
        Tool("corel_export_png", "Export the active page or current selection to an explicit PNG path without opening a dialog. Resolution is DPI; transparent background is supported.", Obj(("path", String(1)), ("range", Enum("page","selection")), ("resolution_dpi", Integer(48,1200)), ("transparent", Bool())),
            async (a, ct) => ToolExecutionResult.Ok(await corel.ExportRasterAsync(S(a,"path"), "png", S(a,"range"), I(a,"resolution_dpi"), B(a,"transparent"), 90, ct))),
        Tool("corel_export_jpeg", "Export the active page or current selection to an explicit JPEG path without opening a dialog. JPEG has no transparency. Quality is 1 to 100.", Obj(("path", String(1)), ("range", Enum("page","selection")), ("resolution_dpi", Integer(48,1200)), ("quality", Integer(1,100))),
            async (a, ct) => ToolExecutionResult.Ok(await corel.ExportRasterAsync(S(a,"path"), "jpeg", S(a,"range"), I(a,"resolution_dpi"), false, I(a,"quality"), ct))),
        Tool("corel_export_svg", "Export the active page or current selection to an explicit SVG path while preserving vector output where CorelDRAW supports it.", Obj(("path", String(1)), ("range", Enum("page","selection"))),
            async (a, ct) => ToolExecutionResult.Ok(await corel.ExportSvgAsync(S(a,"path"), S(a,"range"), ct))),
        Tool(
            "corel_batch_export_pages",
            "Export a contiguous page range to separate PNG, JPEG, or SVG files in an explicit existing directory. File names are deterministic, the original active page is restored, and existing files are never replaced unless overwrite=true. Set overwrite=true only when the user clearly intends to replace existing files.",
            Obj(
                ("output_directory", String(1)),
                ("base_name", String(1)),
                ("format", Enum("png", "jpeg", "svg")),
                ("start_page", Integer(1, 100000)),
                ("end_page", Integer(1, 100000)),
                ("resolution_dpi", Integer(48, 1200)),
                ("transparent", Bool()),
                ("jpeg_quality", Integer(1, 100)),
                ("include_page_name", Bool()),
                ("overwrite", Bool())),
            async (a, ct) => ToolExecutionResult.Ok(await corel.BatchExportPagesAsync(
                S(a, "output_directory"),
                S(a, "base_name"),
                S(a, "format"),
                I(a, "start_page"),
                I(a, "end_page"),
                I(a, "resolution_dpi"),
                B(a, "transparent"),
                I(a, "jpeg_quality"),
                B(a, "include_page_name"),
                B(a, "overwrite"),
                ct)))
    ];

    private static IAiTool Tool(string name, string description, JsonObject parameters,
        Func<JsonObject, CancellationToken, ValueTask<ToolExecutionResult>> execute) =>
        new DelegateTool(new ToolDefinition(name, description, parameters), execute);

    private static JsonObject Bool() => new() { ["type"] = "boolean" };
    private static JsonObject String(int minimumLength = 0) => new() { ["type"] = "string", ["minLength"] = minimumLength };
    private static JsonObject Integer(int minimum, int maximum) => new() { ["type"] = "integer", ["minimum"] = minimum, ["maximum"] = maximum };

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

    private static int I(JsonObject arguments, string key) => arguments[key]?.GetValue<int>() ?? throw new ArgumentException($"Missing {key}");
    private static bool B(JsonObject arguments, string key) => arguments[key]?.GetValue<bool>() ?? throw new ArgumentException($"Missing {key}");
    private static string S(JsonObject arguments, string key) => arguments[key]?.GetValue<string>() ?? throw new ArgumentException($"Missing {key}");

    private sealed class DelegateTool(
        ToolDefinition definition,
        Func<JsonObject, CancellationToken, ValueTask<ToolExecutionResult>> execute) : IAiTool
    {
        public ToolDefinition Definition { get; } = definition;
        public ValueTask<ToolExecutionResult> ExecuteAsync(JsonObject arguments, CancellationToken cancellationToken = default) => execute(arguments, cancellationToken);
    }
}
