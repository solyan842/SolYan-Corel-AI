using System.Text.Json.Nodes;

namespace SolYan.CorelAI.Corel;

public sealed partial class CorelSession
{
    public Task UngroupSelectionAsync(CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            dynamic range = App().ActiveDocument.ActiveSelectionRange;
            EnsureSelection(range);
            range.Ungroup();
        }, cancellationToken);

    public Task ConvertSelectionToCurvesAsync(CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            dynamic range = App().ActiveDocument.ActiveSelectionRange;
            EnsureSelection(range);
            range.ConvertToCurves();
        }, cancellationToken);

    public Task CombineSelectionAsync(CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            dynamic range = App().ActiveDocument.ActiveSelectionRange;
            EnsureSelection(range);
            if (Convert.ToInt32(range.Count) < 2)
                throw new InvalidOperationException("At least two selected shapes are required to combine.");
            range.Combine().CreateSelection();
        }, cancellationToken);

    public Task FlipSelectionAsync(string axis, CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            dynamic range = App().ActiveDocument.ActiveSelectionRange;
            EnsureSelection(range);
            var axes = axis.Trim().ToLowerInvariant() switch
            {
                "horizontal" => 1,
                "vertical" => 2,
                "both" => 3,
                _ => throw new ArgumentException("Axis must be horizontal, vertical, or both.", nameof(axis))
            };
            range.Flip(axes);
        }, cancellationToken);

    public Task DuplicateSelectionGridAsync(
        int rows,
        int columns,
        double offsetXmm,
        double offsetYmm,
        CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            if (rows < 1 || rows > 20) throw new ArgumentOutOfRangeException(nameof(rows), "Rows must be between 1 and 20.");
            if (columns < 1 || columns > 20) throw new ArgumentOutOfRangeException(nameof(columns), "Columns must be between 1 and 20.");
            if ((long)rows * columns > 100) throw new ArgumentOutOfRangeException(nameof(columns), "A duplicate grid is limited to 100 cells.");

            dynamic app = App();
            dynamic doc = app.ActiveDocument;
            dynamic source = doc.ActiveSelectionRange;
            EnsureSelection(source);
            var dx = ToDocumentUnits(app, doc, offsetXmm);
            var dy = ToDocumentUnits(app, doc, offsetYmm);

            source.CreateSelection();
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    if (row == 0 && column == 0) continue;
                    dynamic duplicate = source.Duplicate(column * dx, row * dy);
                    duplicate.AddToSelection();
                }
            }
        }, cancellationToken);

    public Task MoveSelectionToLayerAsync(string layerName, CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            var name = RequireName(layerName, nameof(layerName));
            dynamic doc = App().ActiveDocument;
            dynamic range = doc.ActiveSelectionRange;
            EnsureSelection(range);
            dynamic layer = doc.ActivePage.Layers.Find(name);
            if (layer is null) throw new InvalidOperationException($"Layer '{name}' was not found on the active page.");
            range.MoveToLayer(layer);
        }, cancellationToken);

    public Task<JsonObject> BooleanShapesByIdAsync(
        int sourceStaticId,
        int targetStaticId,
        string operation,
        bool leaveSource,
        bool leaveTarget,
        CancellationToken cancellationToken = default) =>
        _dispatcher.InvokeAsync(() =>
        {
            if (sourceStaticId == targetStaticId)
                throw new ArgumentException("Source and target StaticIDs must identify different shapes.");

            dynamic app = App();
            if (app.Documents.Count == 0) throw new InvalidOperationException("No CorelDRAW document is open.");
            dynamic doc = app.ActiveDocument;
            dynamic page = doc.ActivePage;
            dynamic source = FindShapeByStaticId(page, sourceStaticId);
            dynamic target = FindShapeByStaticId(page, targetStaticId);
            dynamic result;

            switch (operation.Trim().ToLowerInvariant())
            {
                case "weld":
                    result = source.Weld(target, leaveSource, leaveTarget);
                    break;
                case "intersect":
                    result = source.Intersect(target, leaveSource, leaveTarget);
                    break;
                case "trim":
                    result = source.Trim(target, leaveSource, leaveTarget);
                    break;
                default:
                    throw new ArgumentException("Operation must be weld, intersect, or trim.", nameof(operation));
            }

            if (result is null) throw new InvalidOperationException("CorelDRAW did not return a result shape for the boolean operation.");
            result.CreateSelection();
            app.Refresh();

            return new JsonObject
            {
                ["operation"] = operation.Trim().ToLowerInvariant(),
                ["result_static_id"] = TryInt(() => result.StaticID),
                ["source_static_id"] = sourceStaticId,
                ["target_static_id"] = targetStaticId
            };
        }, cancellationToken);

    public Task AddShapesToPowerClipAsync(
        IReadOnlyCollection<int> contentStaticIds,
        int containerStaticId,
        bool centerInContainer,
        CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            if (contentStaticIds.Count == 0) throw new ArgumentException("At least one content StaticID is required.", nameof(contentStaticIds));
            if (contentStaticIds.Contains(containerStaticId)) throw new ArgumentException("The PowerClip container cannot also be content.");

            dynamic app = App();
            dynamic doc = app.ActiveDocument;
            dynamic page = doc.ActivePage;
            dynamic container = FindShapeByStaticId(page, containerStaticId);
            dynamic content = app.CreateShapeRange();
            var wanted = new HashSet<int>(contentStaticIds);
            dynamic all = page.FindShapes();

            for (var i = 1; i <= Convert.ToInt32(all.Count); i++)
            {
                dynamic shape = all[i];
                var id = TryInt(() => shape.StaticID);
                if (id is not null && wanted.Contains(id.Value)) content.Add(shape);
            }

            if (Convert.ToInt32(content.Count) != wanted.Count)
                throw new InvalidOperationException("One or more PowerClip content StaticIDs were not found on the active page.");

            content.AddToPowerClip(container, centerInContainer ? -1 : 0);
            container.CreateSelection();
        }, cancellationToken);

    public Task<JsonObject> ExtractPowerClipAsync(int containerStaticId, CancellationToken cancellationToken = default) =>
        _dispatcher.InvokeAsync(() =>
        {
            dynamic app = App();
            if (app.Documents.Count == 0) throw new InvalidOperationException("No CorelDRAW document is open.");
            dynamic doc = app.ActiveDocument;
            dynamic container = FindShapeByStaticId(doc.ActivePage, containerStaticId);
            dynamic extracted = container.PowerClip.ExtractShapes();
            var count = Convert.ToInt32(extracted.Count);
            if (count > 0) extracted.CreateSelection();
            app.Refresh();

            return new JsonObject
            {
                ["container_static_id"] = containerStaticId,
                ["extracted_count"] = count
            };
        }, cancellationToken);
}
