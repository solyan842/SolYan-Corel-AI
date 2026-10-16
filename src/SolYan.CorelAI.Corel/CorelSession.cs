using System.Text.Json.Nodes;

namespace SolYan.CorelAI.Corel;

public sealed class CorelSession : IDisposable
{
    private readonly StaComDispatcher _dispatcher = new();
    private object? _application;
    private bool _commandGroupOpen;

    public Task ConnectAsync(CancellationToken cancellationToken = default) =>
        _dispatcher.InvokeAsync(() =>
        {
            dynamic app = App();
            app.Visible = true;
        }, cancellationToken);

    public Task<bool> BeginAiJobAsync(string name, CancellationToken cancellationToken = default) =>
        _dispatcher.InvokeAsync(() =>
        {
            dynamic app = App();
            if (app.Documents.Count == 0) return false;
            dynamic doc = app.ActiveDocument;
            doc.BeginCommandGroup(name);
            _commandGroupOpen = true;
            return true;
        }, cancellationToken);

    public Task EndAiJobAsync(CancellationToken cancellationToken = default) =>
        _dispatcher.InvokeAsync(() =>
        {
            if (!_commandGroupOpen) return;
            dynamic app = App();
            app.ActiveDocument.EndCommandGroup();
            app.Refresh();
            _commandGroupOpen = false;
        }, cancellationToken);

    public Task<JsonObject> GetDocumentStateAsync(CancellationToken cancellationToken = default) =>
        _dispatcher.InvokeAsync(() =>
        {
            dynamic app = App();
            if (app.Documents.Count == 0)
                return new JsonObject { ["open"] = false };

            dynamic doc = app.ActiveDocument;
            dynamic page = doc.ActivePage;
            dynamic selection = doc.ActiveSelectionRange;

            return new JsonObject
            {
                ["open"] = true,
                ["name"] = Convert.ToString(doc.Name),
                ["path"] = Convert.ToString(doc.FilePath),
                ["page_count"] = Convert.ToInt32(doc.Pages.Count),
                ["active_page"] = Convert.ToInt32(page.Index),
                ["page_width"] = Convert.ToDouble(page.SizeWidth),
                ["page_height"] = Convert.ToDouble(page.SizeHeight),
                ["selection_count"] = Convert.ToInt32(selection.Count)
            };
        }, cancellationToken);

    public Task<JsonArray> GetSelectionAsync(CancellationToken cancellationToken = default) =>
        _dispatcher.InvokeAsync(() =>
        {
            dynamic doc = App().ActiveDocument;
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
                    ["x"] = TryDouble(() => shape.PositionX),
                    ["y"] = TryDouble(() => shape.PositionY),
                    ["width"] = TryDouble(() => shape.SizeWidth),
                    ["height"] = TryDouble(() => shape.SizeHeight)
                };

                var text = TryString(() => shape.Text.Story.Text);
                if (!string.IsNullOrWhiteSpace(text)) item["text"] = text;
                result.Add(item);
            }

            return result;
        }, cancellationToken);

    public Task SelectAllAsync(CancellationToken cancellationToken = default) =>
        MutateAsync(() => App().ActiveDocument.ActivePage.Shapes.All().CreateSelection(), cancellationToken);

    public Task MoveSelectionAsync(double dx, double dy, CancellationToken cancellationToken = default) =>
        MutateAsync(() => App().ActiveDocument.ActiveSelectionRange.Move(dx, dy), cancellationToken);

    public Task ResizeSelectionAsync(double width, double height, bool preserveAspect, CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            dynamic range = App().ActiveDocument.ActiveSelectionRange;
            if (Convert.ToInt32(range.Count) == 0) throw new InvalidOperationException("Nothing is selected.");

            var currentWidth = Convert.ToDouble(range.SizeWidth);
            var currentHeight = Convert.ToDouble(range.SizeHeight);
            if (preserveAspect)
            {
                if (width > 0 && height <= 0) height = currentHeight * (width / currentWidth);
                else if (height > 0 && width <= 0) width = currentWidth * (height / currentHeight);
                else if (width > 0 && height > 0)
                {
                    var scale = Math.Min(width / currentWidth, height / currentHeight);
                    width = currentWidth * scale;
                    height = currentHeight * scale;
                }
            }

            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Width and height must resolve to positive values.");
            range.SetSize(width, height);
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

    public Task SetOutlineRgbAsync(int r, int g, int b, double width, CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            dynamic app = App();
            dynamic color = app.CreateRGBColor(ClampByte(r), ClampByte(g), ClampByte(b));
            dynamic range = app.ActiveDocument.ActiveSelectionRange;
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

    public Task CreateRectangleAsync(double x, double y, double width, double height, CancellationToken cancellationToken = default) =>
        MutateAsync(() => App().ActiveDocument.ActiveLayer.CreateRectangle2(x, y, width, height).CreateSelection(), cancellationToken);

    public Task CreateArtisticTextAsync(double x, double y, string text, CancellationToken cancellationToken = default) =>
        MutateAsync(() => App().ActiveDocument.ActiveLayer.CreateArtisticText(x, y, text).CreateSelection(), cancellationToken);

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

    private static int ClampByte(int value) => Math.Clamp(value, 0, 255);

    private static string? TryString(Func<object?> getter)
    {
        try { return Convert.ToString(getter()); }
        catch { return null; }
    }

    private static double? TryDouble(Func<object?> getter)
    {
        try { return Convert.ToDouble(getter()); }
        catch { return null; }
    }

    public void Dispose() => _dispatcher.Dispose();
}
