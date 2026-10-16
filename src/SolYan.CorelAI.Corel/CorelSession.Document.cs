using System.Text.Json.Nodes;

namespace SolYan.CorelAI.Corel;

public sealed partial class CorelSession
{
    public Task<JsonObject> GetPagesAndLayersAsync(CancellationToken cancellationToken = default) =>
        _dispatcher.InvokeAsync(() =>
        {
            dynamic app = App();
            if (app.Documents.Count == 0)
                return new JsonObject { ["open"] = false };

            dynamic doc = app.ActiveDocument;
            var activePageIndex = Convert.ToInt32(doc.ActivePage.Index);
            var activeLayerName = TryString(() => doc.ActiveLayer.Name);
            var pages = new JsonArray();

            for (var pageIndex = 1; pageIndex <= Convert.ToInt32(doc.Pages.Count); pageIndex++)
            {
                dynamic page = doc.Pages[pageIndex];
                var layers = new JsonArray();
                for (var layerIndex = 1; layerIndex <= Convert.ToInt32(page.Layers.Count); layerIndex++)
                {
                    dynamic layer = page.Layers[layerIndex];
                    var layerName = TryString(() => layer.Name) ?? $"Layer {layerIndex}";
                    layers.Add(new JsonObject
                    {
                        ["index"] = layerIndex,
                        ["name"] = layerName,
                        ["visible"] = TryBool(() => layer.Visible),
                        ["editable"] = TryBool(() => layer.Editable),
                        ["active"] = pageIndex == activePageIndex &&
                                     string.Equals(layerName, activeLayerName, StringComparison.OrdinalIgnoreCase)
                    });
                }

                pages.Add(new JsonObject
                {
                    ["index"] = pageIndex,
                    ["name"] = TryString(() => page.Name),
                    ["active"] = pageIndex == activePageIndex,
                    ["layers"] = layers
                });
            }

            return new JsonObject
            {
                ["open"] = true,
                ["title"] = TryString(() => doc.Title),
                ["full_file_name"] = TryString(() => doc.FullFileName),
                ["active_page"] = activePageIndex,
                ["active_layer"] = activeLayerName,
                ["pages"] = pages
            };
        }, cancellationToken);

    public Task ActivatePageAsync(int pageIndex, CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            dynamic doc = App().ActiveDocument;
            var count = Convert.ToInt32(doc.Pages.Count);
            if (pageIndex < 1 || pageIndex > count)
                throw new ArgumentOutOfRangeException(nameof(pageIndex), $"Page index must be between 1 and {count}.");
            doc.Pages[pageIndex].Activate();
        }, cancellationToken);

    public Task CreateLayerAsync(string name, bool activate, CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            var normalizedName = RequireName(name, nameof(name));
            dynamic page = App().ActiveDocument.ActivePage;
            dynamic layer = page.CreateLayer(normalizedName);
            if (activate) layer.Activate();
        }, cancellationToken);

    public Task ActivateLayerAsync(string name, CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            var normalizedName = RequireName(name, nameof(name));
            dynamic page = App().ActiveDocument.ActivePage;
            dynamic layer = page.Layers[normalizedName];
            layer.Activate();
        }, cancellationToken);

    public Task SetLayerStateAsync(string name, bool visible, bool editable, CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            var normalizedName = RequireName(name, nameof(name));
            dynamic page = App().ActiveDocument.ActivePage;
            dynamic layer = page.Layers[normalizedName];
            layer.Visible = visible;
            layer.Editable = editable;
        }, cancellationToken);

    public Task ImportFileAsync(string path, CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            var fullPath = RequireExistingFile(path);
            dynamic doc = App().ActiveDocument;
            doc.ActiveLayer.Import(fullPath);
        }, cancellationToken);

    public Task SaveAsync(CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            dynamic doc = App().ActiveDocument;
            var fullFileName = Convert.ToString(doc.FullFileName);
            if (string.IsNullOrWhiteSpace(fullFileName))
                throw new InvalidOperationException("This document has never been saved. Use Save As with an explicit path.");
            doc.Save();
        }, cancellationToken);

    public Task SaveAsAsync(string path, CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            var fullPath = RequireWritableTarget(path);
            dynamic doc = App().ActiveDocument;
            doc.SaveAs(fullPath);
        }, cancellationToken);

    private static string RequireName(string name, string parameterName)
    {
        var value = name?.Trim();
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Name cannot be empty.", parameterName);
        return value;
    }

    private static string RequireExistingFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be empty.", nameof(path));
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("File does not exist.", fullPath);
        return fullPath;
    }

    private static string RequireWritableTarget(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be empty.", nameof(path));
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Target directory does not exist: {directory}");
        return fullPath;
    }

    private static bool? TryBool(Func<object?> getter)
    {
        try { return Convert.ToBoolean(getter()); }
        catch { return null; }
    }
}
