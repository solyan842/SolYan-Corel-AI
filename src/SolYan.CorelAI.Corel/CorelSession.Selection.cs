using System.Text.Json.Nodes;

namespace SolYan.CorelAI.Corel;

public sealed partial class CorelSession
{
    public Task<JsonObject> ListPageShapesAsync(int limit = 250, CancellationToken cancellationToken = default) =>
        _dispatcher.InvokeAsync(() =>
        {
            if (limit < 1 || limit > 2000)
                throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 2000.");

            dynamic app = App();
            if (app.Documents.Count == 0)
                return new JsonObject { ["open"] = false, ["shapes"] = new JsonArray() };

            dynamic doc = app.ActiveDocument;
            dynamic shapes = doc.ActivePage.FindShapes();
            var total = Convert.ToInt32(shapes.Count);
            var result = new JsonArray();

            for (var i = 1; i <= Math.Min(total, limit); i++)
                result.Add(DescribeShape((object)app, (object)doc, shapes[i]));

            return new JsonObject
            {
                ["open"] = true,
                ["page_index"] = Convert.ToInt32(doc.ActivePage.Index),
                ["total"] = total,
                ["returned"] = result.Count,
                ["truncated"] = total > limit,
                ["shapes"] = result
            };
        }, cancellationToken);

    public Task SelectShapesByStaticIdsAsync(IReadOnlyCollection<int> staticIds, CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            if (staticIds.Count == 0) throw new ArgumentException("At least one StaticID is required.", nameof(staticIds));
            if (staticIds.Count > 500) throw new ArgumentOutOfRangeException(nameof(staticIds), "At most 500 StaticIDs may be selected at once.");

            dynamic app = App();
            dynamic doc = app.ActiveDocument;
            dynamic all = doc.ActivePage.FindShapes();
            dynamic selected = app.CreateShapeRange();
            var wanted = new HashSet<int>(staticIds);

            for (var i = 1; i <= Convert.ToInt32(all.Count); i++)
            {
                dynamic shape = all[i];
                var id = TryInt(() => shape.StaticID);
                if (id is not null && wanted.Contains(id.Value)) selected.Add(shape);
            }

            if (Convert.ToInt32(selected.Count) == 0)
                throw new InvalidOperationException("No matching shapes were found on the active page.");

            selected.CreateSelection();
        }, cancellationToken);

    public Task<JsonObject> SelectMatchingShapesAsync(
        string? nameContains,
        string? textContains,
        string? layerName,
        bool caseSensitive,
        CancellationToken cancellationToken = default) =>
        _dispatcher.InvokeAsync(() =>
        {
            var hasName = !string.IsNullOrWhiteSpace(nameContains);
            var hasText = !string.IsNullOrWhiteSpace(textContains);
            var hasLayer = !string.IsNullOrWhiteSpace(layerName);
            if (!hasName && !hasText && !hasLayer)
                throw new ArgumentException("At least one of name_contains, text_contains, or layer_name is required.");

            dynamic app = App();
            if (app.Documents.Count == 0) throw new InvalidOperationException("No CorelDRAW document is open.");
            dynamic doc = app.ActiveDocument;
            dynamic all = doc.ActivePage.FindShapes();
            dynamic matches = app.CreateShapeRange();
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            for (var i = 1; i <= Convert.ToInt32(all.Count); i++)
            {
                dynamic shape = all[i];
                var name = TryString(() => shape.Name) ?? string.Empty;
                var text = TryString(() => shape.Text.Story.Text) ?? string.Empty;
                var layer = TryString(() => shape.Layer.Name) ?? string.Empty;

                if (hasName && name.IndexOf(nameContains!, comparison) < 0) continue;
                if (hasText && text.IndexOf(textContains!, comparison) < 0) continue;
                if (hasLayer && !string.Equals(layer, layerName, comparison)) continue;
                matches.Add(shape);
            }

            var count = Convert.ToInt32(matches.Count);
            if (count > 0) matches.CreateSelection();

            return new JsonObject
            {
                ["matched"] = count,
                ["selected"] = count > 0
            };
        }, cancellationToken);

    public Task<JsonObject> FindReplaceTextAsync(
        string find,
        string replacement,
        bool caseSensitive,
        string scope,
        CancellationToken cancellationToken = default) =>
        _dispatcher.InvokeAsync(() =>
        {
            if (string.IsNullOrEmpty(find)) throw new ArgumentException("Find text cannot be empty.", nameof(find));

            dynamic app = App();
            if (app.Documents.Count == 0) throw new InvalidOperationException("No CorelDRAW document is open.");
            dynamic doc = app.ActiveDocument;
            dynamic shapes;

            switch (scope.Trim().ToLowerInvariant())
            {
                case "selection":
                    shapes = app.ActiveSelectionRange;
                    EnsureSelection(shapes);
                    break;
                case "page":
                    shapes = doc.ActivePage.FindShapes();
                    break;
                default:
                    throw new ArgumentException("Scope must be 'selection' or 'page'.", nameof(scope));
            }

            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var changedShapes = 0;
            var replacements = 0;

            for (var i = 1; i <= Convert.ToInt32(shapes.Count); i++)
            {
                dynamic shape = shapes[i];
                string? original;
                try { original = Convert.ToString(shape.Text.Story.Text); }
                catch { continue; }
                if (string.IsNullOrEmpty(original)) continue;

                var count = CountOccurrences(original, find, comparison);
                if (count == 0) continue;

                shape.Text.Story.Text = original.Replace(find, replacement ?? string.Empty, comparison);
                changedShapes++;
                replacements += count;
            }

            app.Refresh();
            return new JsonObject
            {
                ["changed_shapes"] = changedShapes,
                ["replacements"] = replacements
            };
        }, cancellationToken);

    private static JsonObject DescribeShape(object appObject, object docObject, object shapeObject)
    {
        dynamic app = appObject;
        dynamic doc = docObject;
        dynamic shape = shapeObject;
        var item = new JsonObject
        {
            ["static_id"] = TryInt(() => shape.StaticID),
            ["name"] = TryString(() => shape.Name),
            ["type_id"] = TryInt(() => shape.Type),
            ["layer"] = TryString(() => shape.Layer.Name),
            ["x_mm"] = TryMm(appObject, docObject, () => shape.PositionX),
            ["y_mm"] = TryMm(appObject, docObject, () => shape.PositionY),
            ["width_mm"] = TryMm(appObject, docObject, () => shape.SizeWidth),
            ["height_mm"] = TryMm(appObject, docObject, () => shape.SizeHeight),
            ["rotation_degrees"] = TryDouble(() => shape.RotationAngle),
            ["locked"] = TryBool(() => shape.Locked)
        };

        var text = TryString(() => shape.Text.Story.Text);
        if (!string.IsNullOrWhiteSpace(text)) item["text"] = text;
        return item;
    }

    private dynamic FindShapeByStaticId(dynamic page, int staticId)
    {
        dynamic all = page.FindShapes();
        for (var i = 1; i <= Convert.ToInt32(all.Count); i++)
        {
            dynamic shape = all[i];
            if (TryInt(() => shape.StaticID) == staticId) return shape;
        }

        throw new InvalidOperationException($"Shape StaticID {staticId} was not found on the active page.");
    }

    private static int CountOccurrences(string source, string value, StringComparison comparison)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, comparison)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }
}
