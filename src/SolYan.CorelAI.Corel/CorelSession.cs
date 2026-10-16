using System.Text.Json.Nodes;

namespace SolYan.CorelAI.Corel;

public sealed class CorelSession : IDisposable
{
    private const int CdrMillimeter = 3;

    private readonly StaComDispatcher _dispatcher = new();
    private object? _application;
    private object? _commandGroupDocument;
    private bool _commandGroupOpen;

    public Task ConnectAsync(CancellationToken cancellationToken = default) =>
        _dispatcher.InvokeAsync(() =>
        {
            dynamic app = App();
            app.Visible = true;
        }, cancellationToken);

    public Task<JsonObject> GetCapabilitiesAsync(CancellationToken cancellationToken = default) =>
        _dispatcher.InvokeAsync(() =>
        {
            dynamic app = App();
            var documentCount = TryInt(() => app.Documents.Count) ?? 0;
            var hasDocument = documentCount > 0;

            var features = new JsonObject
            {
                ["application_convert_units"] = Probe(() => app.ConvertUnits(1d, CdrMillimeter, CdrMillimeter)),
                ["document_to_units"] = hasDocument && Probe(() => app.ActiveDocument.ToUnits(1d, CdrMillimeter)),
                ["command_groups"] = hasDocument,
                ["rgb_color_factory"] = Probe(() => app.CreateRGBColor(0, 0, 0)),
                ["pdf_publish"] = hasDocument,
                ["late_bound_com"] = true,
                ["sta_dispatcher"] = true,
                ["ai_geometry_unit"] = "millimetre"
            };

            var result = new JsonObject
            {
                ["connected"] = true,
                ["application"] = TryString(() => app.Name) ?? "CorelDRAW",
                ["version"] = TryString(() => app.Version),
                ["version_major"] = TryInt(() => app.VersionMajor),
                ["version_build"] = TryInt(() => app.VersionBuild),
                ["documents_open"] = documentCount,
                ["features"] = features
            };

            if (hasDocument)
            {
                dynamic doc = app.ActiveDocument;
                var unit = TryInt(() => doc.Unit);
                result["active_document_unit_id"] = unit;
                result["active_document_unit"] = unit is null ? null : UnitName(unit.Value);
            }

            return result;
        }, cancellationToken);

    public Task<bool> BeginAiJobAsync(string name, CancellationToken cancellationToken = default) =>
        _dispatcher.InvokeAsync(() =>
        {
            if (_commandGroupOpen)
                throw new InvalidOperationException("An AI command group is already open.");

            dynamic app = App();
            if (app.Documents.Count == 0) return false;

            dynamic doc = app.ActiveDocument;
            doc.BeginCommandGroup(name);
            _commandGroupDocument = doc;
            _commandGroupOpen = true;
            return true;
        }, cancellationToken);

    public Task EndAiJobAsync(CancellationToken cancellationToken = default) =>
        _dispatcher.InvokeAsync(() =>
        {
            if (!_commandGroupOpen) return;

            dynamic app = App();
            try
            {
                dynamic doc = _commandGroupDocument ?? app.ActiveDocument;
                doc.EndCommandGroup();
            }
            finally
            {
                _commandGroupOpen = false;
                _commandGroupDocument = null;
                app.Refresh();
            }
        }, cancellationToken);

    public Task<JsonObject> GetDocumentStateAsync(CancellationToken cancellationToken = default) =>
        _dispatcher.InvokeAsync(() =>
        {
            dynamic app = App();
            if (app.Documents.Count == 0)
                return new JsonObject { ["open"] = false, ["geometry_unit"] = "mm" };

            dynamic doc = app.ActiveDocument;
            dynamic page = doc.ActivePage;
            dynamic selection = doc.ActiveSelectionRange;
            var unit = Convert.ToInt32(doc.Unit);

            return new JsonObject
            {
                ["open"] = true,
                ["name"] = Convert.ToString(doc.Name),
                ["path"] = Convert.ToString(doc.FilePath),
                ["page_count"] = Convert.ToInt32(doc.Pages.Count),
                ["active_page"] = Convert.ToInt32(page.Index),
                ["page_width_mm"] = ToMm(app, doc, Convert.ToDouble(page.SizeWidth)),
                ["page_height_mm"] = ToMm(app, doc, Convert.ToDouble(page.SizeHeight)),
                ["drawing_origin_x_mm"] = ToMm(app, doc, Convert.ToDouble(doc.DrawingOriginX)),
                ["drawing_origin_y_mm"] = ToMm(app, doc, Convert.ToDouble(doc.DrawingOriginY)),
                ["selection_count"] = Convert.ToInt32(selection.Count),
                ["document_unit_id"] = unit,
                ["document_unit"] = UnitName(unit),
                ["geometry_unit"] = "mm"
            };
        }, cancellationToken);

    public Task<JsonArray> GetSelectionAsync(CancellationToken cancellationToken = default) =>
        _dispatcher.InvokeAsync(() =>
        {
            dynamic app = App();
            dynamic doc = app.ActiveDocument;
            dynamic range = doc.ActiveSelectionRange;
            var result = new JsonArray();

            for (var i = 1; i <= Convert.ToInt32(range.Count); i++)
            {
                dynamic shape = range[i];
                var item = new JsonObject
                {
                    ["index"] = i,
                    ["name"] = TryString(() => shape.Name),
                    ["type"] = TryString(() => shape.Type),
                    ["x_mm"] = TryMm(app, doc, () => shape.PositionX),
                    ["y_mm"] = TryMm(app, doc, () => shape.PositionY),
                    ["width_mm"] = TryMm(app, doc, () => shape.SizeWidth),
                    ["height_mm"] = TryMm(app, doc, () => shape.SizeHeight),
                    ["rotation_degrees"] = TryDouble(() => shape.RotationAngle)
                };

                var text = TryString(() => shape.Text.Story.Text);
                if (!string.IsNullOrWhiteSpace(text)) item["text"] = text;
                result.Add(item);
            }

            return result;
        }, cancellationToken);

    public Task SelectAllAsync(CancellationToken cancellationToken = default) =>
        MutateAsync(() => App().ActiveDocument.ActivePage.Shapes.All().CreateSelection(), cancellationToken);

    public Task MoveSelectionAsync(double dxMm, double dyMm, CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            dynamic app = App();
            dynamic doc = app.ActiveDocument;
            doc.ActiveSelectionRange.Move(ToDocumentUnits(app, doc, dxMm), ToDocumentUnits(app, doc, dyMm));
        }, cancellationToken);

    public Task ResizeSelectionAsync(double widthMm, double heightMm, bool preserveAspect, CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            dynamic app = App();
            dynamic doc = app.ActiveDocument;
            dynamic range = doc.ActiveSelectionRange;
            if (Convert.ToInt32(range.Count) == 0) throw new InvalidOperationException("Nothing is selected.");

            var currentWidthMm = ToMm(app, doc, Convert.ToDouble(range.SizeWidth));
            var currentHeightMm = ToMm(app, doc, Convert.ToDouble(range.SizeHeight));

            if (preserveAspect)
            {
                if (widthMm > 0 && heightMm <= 0) heightMm = currentHeightMm * (widthMm / currentWidthMm);
                else if (heightMm > 0 && widthMm <= 0) widthMm = currentWidthMm * (heightMm / currentHeightMm);
                else if (widthMm > 0 && heightMm > 0)
                {
                    var scale = Math.Min(widthMm / currentWidthMm, heightMm / currentHeightMm);
                    widthMm = currentWidthMm * scale;
                    heightMm = currentHeightMm * scale;
                }
            }

            if (widthMm <= 0 || heightMm <= 0)
                throw new ArgumentOutOfRangeException(nameof(widthMm), "Width and height must resolve to positive millimetre values.");

            range.SetSize(ToDocumentUnits(app, doc, widthMm), ToDocumentUnits(app, doc, heightMm));
        }, cancellationToken);

    public Task CenterSelectionOnPageAsync(CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            dynamic doc = App().ActiveDocument;
            dynamic range = doc.ActiveSelectionRange;
            if (Convert.ToInt32(range.Count) == 0) throw new InvalidOperationException("Nothing is selected.");

            double x = 0, y = 0, w = 0, h = 0;
            range.GetBoundingBox(ref x, ref y, ref w, ref h);
            var targetX = Convert.ToDouble(doc.ActivePage.SizeWidth) / 2.0;
            var targetY = Convert.ToDouble(doc.ActivePage.SizeHeight) / 2.0;
            range.Move(targetX - (x + w / 2.0), targetY - (y + h / 2.0));
        }, cancellationToken);

    public Task SetFillRgbAsync(int r, int g, int b, CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            dynamic app = App();
            dynamic color = app.CreateRGBColor(ClampByte(r), ClampByte(g), ClampByte(b));
            dynamic range = app.ActiveDocument.ActiveSelectionRange;
            for (var i = 1; i <= Convert.ToInt32(range.Count); i++) range[i].Fill.ApplyUniformFill(color);
        }, cancellationToken);

    public Task SetOutlineRgbAsync(int r, int g, int b, double widthMm, CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            if (widthMm < 0) throw new ArgumentOutOfRangeException(nameof(widthMm));

            dynamic app = App();
            dynamic doc = app.ActiveDocument;
            dynamic color = app.CreateRGBColor(ClampByte(r), ClampByte(g), ClampByte(b));
            dynamic range = doc.ActiveSelectionRange;
            var width = ToDocumentUnits(app, doc, widthMm);

            for (var i = 1; i <= Convert.ToInt32(range.Count); i++)
            {
                range[i].Outline.Color = color;
                range[i].Outline.Width = width;
            }
        }, cancellationToken);

    public Task SetSelectedTextAsync(string text, CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            dynamic range = App().ActiveDocument.ActiveSelectionRange;
            var changed = 0;
            for (var i = 1; i <= Convert.ToInt32(range.Count); i++)
            {
                try
                {
                    range[i].Text.Story.Text = text;
                    changed++;
                }
                catch
                {
                    // Non-text shapes are intentionally skipped.
                }
            }
            if (changed == 0) throw new InvalidOperationException("The current selection contains no editable text shape.");
        }, cancellationToken);

    public Task CreateRectangleAsync(double xMm, double yMm, double widthMm, double heightMm, CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            if (widthMm <= 0 || heightMm <= 0) throw new ArgumentOutOfRangeException(nameof(widthMm));

            dynamic app = App();
            dynamic doc = app.ActiveDocument;
            doc.ActiveLayer.CreateRectangle2(
                ToDocumentUnits(app, doc, xMm),
                ToDocumentUnits(app, doc, yMm),
                ToDocumentUnits(app, doc, widthMm),
                ToDocumentUnits(app, doc, heightMm)).CreateSelection();
        }, cancellationToken);

    public Task CreateArtisticTextAsync(double xMm, double yMm, string text, CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            dynamic app = App();
            dynamic doc = app.ActiveDocument;
            doc.ActiveLayer.CreateArtisticText(
                ToDocumentUnits(app, doc, xMm),
                ToDocumentUnits(app, doc, yMm),
                text).CreateSelection();
        }, cancellationToken);

    public Task DuplicateSelectionAsync(CancellationToken cancellationToken = default) =>
        MutateAsync(() => App().ActiveDocument.ActiveSelectionRange.Duplicate().CreateSelection(), cancellationToken);

    public Task GroupSelectionAsync(CancellationToken cancellationToken = default) =>
        MutateAsync(() => App().ActiveDocument.ActiveSelectionRange.Group().CreateSelection(), cancellationToken);

    public Task DeleteSelectionAsync(CancellationToken cancellationToken = default) =>
        MutateAsync(() => App().ActiveDocument.ActiveSelectionRange.Delete(), cancellationToken);

    public Task PublishPdfAsync(string path, CancellationToken cancellationToken = default) =>
        MutateAsync(() => App().ActiveDocument.PublishToPDF(Path.GetFullPath(path)), cancellationToken);

    public Task UndoAsync(CancellationToken cancellationToken = default) =>
        MutateAsync(() => App().ActiveDocument.Undo(), cancellationToken);

    private dynamic App() => _application ??= ComRunningObject.GetOrCreate("CorelDRAW.Application");

    private Task MutateAsync(Action action, CancellationToken cancellationToken) =>
        _dispatcher.InvokeAsync(() =>
        {
            dynamic app = App();
            if (app.Documents.Count == 0) throw new InvalidOperationException("No CorelDRAW document is open.");
            action();
            app.Refresh();
        }, cancellationToken);

    private static double ToDocumentUnits(dynamic app, dynamic doc, double millimetres)
    {
        try
        {
            return Convert.ToDouble(doc.ToUnits(millimetres, CdrMillimeter));
        }
        catch
        {
            return Convert.ToDouble(app.ConvertUnits(millimetres, CdrMillimeter, Convert.ToInt32(doc.Unit)));
        }
    }

    private static double ToMm(dynamic app, dynamic doc, double documentValue) =>
        Convert.ToDouble(app.ConvertUnits(documentValue, Convert.ToInt32(doc.Unit), CdrMillimeter));

    private static int ClampByte(int value) => Math.Clamp(value, 0, 255);

    private static bool Probe(Func<object?> probe)
    {
        try
        {
            _ = probe();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string UnitName(int unit) => unit switch
    {
        0 => "tenth-micron",
        1 => "inch",
        2 => "foot",
        3 => "millimetre",
        4 => "centimetre",
        5 => "pixel",
        6 => "mile",
        7 => "metre",
        8 => "kilometre",
        9 => "didot",
        10 => "agate",
        11 => "yard",
        12 => "pica",
        13 => "cicero",
        14 => "point",
        15 => "Q",
        16 => "H",
        _ => $"unknown-{unit}"
    };

    private static string? TryString(Func<object?> getter)
    {
        try { return Convert.ToString(getter()); }
        catch { return null; }
    }

    private static int? TryInt(Func<object?> getter)
    {
        try { return Convert.ToInt32(getter()); }
        catch { return null; }
    }

    private static double? TryDouble(Func<object?> getter)
    {
        try { return Convert.ToDouble(getter()); }
        catch { return null; }
    }

    private static double? TryMm(dynamic app, dynamic doc, Func<object?> getter)
    {
        try { return ToMm(app, doc, Convert.ToDouble(getter())); }
        catch { return null; }
    }

    public void Dispose() => _dispatcher.Dispose();
}
