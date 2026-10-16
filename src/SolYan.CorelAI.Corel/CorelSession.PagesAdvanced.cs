using System.Text.Json.Nodes;

namespace SolYan.CorelAI.Corel;

public sealed partial class CorelSession
{
    public Task<JsonObject> AddPagesAsync(
        int count,
        double widthMm,
        double heightMm,
        CancellationToken cancellationToken = default) =>
        _dispatcher.InvokeAsync(() =>
        {
            if (count < 1 || count > 50) throw new ArgumentOutOfRangeException(nameof(count), "Count must be between 1 and 50.");
            if (widthMm <= 0 || heightMm <= 0) throw new ArgumentOutOfRangeException(nameof(widthMm), "Page dimensions must be positive.");

            dynamic app = App();
            if (app.Documents.Count == 0) throw new InvalidOperationException("No CorelDRAW document is open.");
            dynamic doc = app.ActiveDocument;
            dynamic page = doc.AddPagesEx(
                count,
                ToDocumentUnits(app, doc, widthMm),
                ToDocumentUnits(app, doc, heightMm));
            app.Refresh();

            return new JsonObject
            {
                ["added"] = count,
                ["first_new_page_index"] = Convert.ToInt32(page.Index),
                ["page_count"] = Convert.ToInt32(doc.Pages.Count)
            };
        }, cancellationToken);

    public Task RenamePageAsync(int pageIndex, string name, CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            var pageName = RequireName(name, nameof(name));
            dynamic doc = App().ActiveDocument;
            ValidatePageIndex(doc, pageIndex);
            doc.Pages[pageIndex].Name = pageName;
        }, cancellationToken);

    public Task DeletePagesAsync(int startPage, int count, CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            dynamic doc = App().ActiveDocument;
            var pageCount = Convert.ToInt32(doc.Pages.Count);
            if (startPage < 1 || startPage > pageCount) throw new ArgumentOutOfRangeException(nameof(startPage));
            if (startPage + count - 1 > pageCount) throw new ArgumentOutOfRangeException(nameof(count), "The requested range extends past the last page.");
            if (count >= pageCount) throw new InvalidOperationException("SolYan Corel AI will not delete every page in a document.");
            doc.DeletePages(startPage, count);
        }, cancellationToken);

    public Task RenameLayerAsync(string currentName, string newName, CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            var sourceName = RequireName(currentName, nameof(currentName));
            var targetName = RequireName(newName, nameof(newName));
            dynamic page = App().ActiveDocument.ActivePage;
            dynamic layer = page.Layers.Find(sourceName);
            if (layer is null) throw new InvalidOperationException($"Layer '{sourceName}' was not found on the active page.");
            layer.Name = targetName;
        }, cancellationToken);

    public Task<JsonObject> DeleteLayerAsync(
        string name,
        bool deleteContents,
        CancellationToken cancellationToken = default) =>
        _dispatcher.InvokeAsync(() =>
        {
            var layerName = RequireName(name, nameof(name));
            if (!deleteContents)
                throw new InvalidOperationException("Deleting a CorelDRAW layer also deletes its contents. Set delete_contents=true only when the user explicitly requested that destructive operation.");

            dynamic app = App();
            if (app.Documents.Count == 0) throw new InvalidOperationException("No CorelDRAW document is open.");
            dynamic doc = app.ActiveDocument;
            dynamic page = doc.ActivePage;
            dynamic layer = page.Layers.Find(layerName);
            if (layer is null) throw new InvalidOperationException($"Layer '{layerName}' was not found on the active page.");
            var shapeCount = Convert.ToInt32(layer.Shapes.Count);
            if (Convert.ToInt32(page.Layers.Count) <= 1)
                throw new InvalidOperationException("SolYan Corel AI will not delete the only layer on a page.");

            layer.Delete();
            app.Refresh();
            return new JsonObject
            {
                ["deleted_layer"] = layerName,
                ["deleted_shapes"] = shapeCount
            };
        }, cancellationToken);

    private static void ValidatePageIndex(dynamic doc, int pageIndex)
    {
        var count = Convert.ToInt32(doc.Pages.Count);
        if (pageIndex < 1 || pageIndex > count)
            throw new ArgumentOutOfRangeException(nameof(pageIndex), $"Page index must be between 1 and {count}.");
    }
}
