using System.Text.Json.Nodes;

namespace SolYan.CorelAI.Corel;

public sealed partial class CorelSession
{
    public Task<JsonObject> FitSelectionToPageAsync(
        double marginMm,
        bool preserveAspect,
        string horizontalAlign,
        string verticalAlign,
        CancellationToken cancellationToken = default) =>
        _dispatcher.InvokeAsync(() =>
        {
            if (marginMm < 0) throw new ArgumentOutOfRangeException(nameof(marginMm));

            dynamic app = App();
            if (app.Documents.Count == 0) throw new InvalidOperationException("No CorelDRAW document is open.");
            dynamic doc = app.ActiveDocument;
            dynamic page = doc.ActivePage;
            dynamic range = app.ActiveSelectionRange;
            EnsureSelection(range);

            var pageWidth = Convert.ToDouble(page.SizeWidth);
            var pageHeight = Convert.ToDouble(page.SizeHeight);
            var margin = ToDocumentUnits(app, doc, marginMm);
            var availableWidth = pageWidth - 2d * margin;
            var availableHeight = pageHeight - 2d * margin;
            if (availableWidth <= 0 || availableHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(marginMm), "Margins leave no usable page area.");

            double x = 0, y = 0, width = 0, height = 0;
            range.GetBoundingBox(ref x, ref y, ref width, ref height);
            if (width <= 0 || height <= 0)
                throw new InvalidOperationException("The selection has no measurable size.");

            if (preserveAspect)
            {
                var scale = Math.Min(availableWidth / width, availableHeight / height);
                range.SetSize(width * scale, height * scale);
            }
            else
            {
                range.SetSize(availableWidth, availableHeight);
            }

            range.GetBoundingBox(ref x, ref y, ref width, ref height);

            var pageCenterX = -Convert.ToDouble(doc.DrawingOriginX);
            var pageCenterY = -Convert.ToDouble(doc.DrawingOriginY);
            var pageLeft = pageCenterX - pageWidth / 2d;
            var pageRight = pageCenterX + pageWidth / 2d;
            var pageBottom = pageCenterY - pageHeight / 2d;
            var pageTop = pageCenterY + pageHeight / 2d;

            var targetLeft = NormalizeHorizontal(horizontalAlign) switch
            {
                "left" => pageLeft + margin,
                "center" => pageCenterX - width / 2d,
                "right" => pageRight - margin - width,
                _ => throw new InvalidOperationException("Unexpected horizontal alignment.")
            };

            var targetBottom = NormalizeVertical(verticalAlign) switch
            {
                "bottom" => pageBottom + margin,
                "center" => pageCenterY - height / 2d,
                "top" => pageTop - margin - height,
                _ => throw new InvalidOperationException("Unexpected vertical alignment.")
            };

            range.Move(targetLeft - x, targetBottom - y);
            range.GetBoundingBox(ref x, ref y, ref width, ref height);
            app.Refresh();

            return new JsonObject
            {
                ["workflow"] = "fit_selection_to_page",
                ["margin_mm"] = marginMm,
                ["preserve_aspect"] = preserveAspect,
                ["horizontal_align"] = NormalizeHorizontal(horizontalAlign),
                ["vertical_align"] = NormalizeVertical(verticalAlign),
                ["result"] = BoundingBoxMm(app, doc, x, y, width, height)
            };
        }, cancellationToken);

    public Task<JsonObject> ArrangeSelectionGridAsync(
        int columns,
        double horizontalGapMm,
        double verticalGapMm,
        CancellationToken cancellationToken = default) =>
        _dispatcher.InvokeAsync(() =>
        {
            if (columns < 1 || columns > 50)
                throw new ArgumentOutOfRangeException(nameof(columns), "Columns must be between 1 and 50.");
            if (horizontalGapMm < 0 || verticalGapMm < 0)
                throw new ArgumentOutOfRangeException(nameof(horizontalGapMm), "Grid gaps cannot be negative.");

            dynamic app = App();
            if (app.Documents.Count == 0) throw new InvalidOperationException("No CorelDRAW document is open.");
            dynamic doc = app.ActiveDocument;
            dynamic range = app.ActiveSelectionRange;
            EnsureSelection(range);

            var count = Convert.ToInt32(range.Count);
            if (count < 2) throw new InvalidOperationException("Select at least two objects to arrange a grid.");

            var items = new List<(object Shape, double X, double Y, double Width, double Height)>();
            for (var i = 1; i <= count; i++)
            {
                dynamic shape = range[i];
                double x = 0, y = 0, width = 0, height = 0;
                shape.GetBoundingBox(ref x, ref y, ref width, ref height);
                items.Add(((object)shape, x, y, width, height));
            }

            items.Sort((a, b) =>
            {
                var topCompare = (b.Y + b.Height).CompareTo(a.Y + a.Height);
                return topCompare != 0 ? topCompare : a.X.CompareTo(b.X);
            });

            columns = Math.Min(columns, count);
            var rows = (int)Math.Ceiling(count / (double)columns);
            var cellWidth = items.Max(item => item.Width);
            var cellHeight = items.Max(item => item.Height);
            var gapX = ToDocumentUnits(app, doc, horizontalGapMm);
            var gapY = ToDocumentUnits(app, doc, verticalGapMm);
            var anchorLeft = items.Min(item => item.X);
            var anchorTop = items.Max(item => item.Y + item.Height);

            for (var index = 0; index < items.Count; index++)
            {
                var row = index / columns;
                var column = index % columns;
                var item = items[index];
                var targetCenterX = anchorLeft + column * (cellWidth + gapX) + cellWidth / 2d;
                var targetCenterY = anchorTop - row * (cellHeight + gapY) - cellHeight / 2d;
                var currentCenterX = item.X + item.Width / 2d;
                var currentCenterY = item.Y + item.Height / 2d;
                dynamic shape = item.Shape;
                shape.Move(targetCenterX - currentCenterX, targetCenterY - currentCenterY);
            }

            double finalX = 0, finalY = 0, finalWidth = 0, finalHeight = 0;
            range.GetBoundingBox(ref finalX, ref finalY, ref finalWidth, ref finalHeight);
            app.Refresh();

            return new JsonObject
            {
                ["workflow"] = "arrange_selection_grid",
                ["object_count"] = count,
                ["rows"] = rows,
                ["columns"] = columns,
                ["horizontal_gap_mm"] = horizontalGapMm,
                ["vertical_gap_mm"] = verticalGapMm,
                ["cell_width_mm"] = ToMm(app, doc, cellWidth),
                ["cell_height_mm"] = ToMm(app, doc, cellHeight),
                ["result"] = BoundingBoxMm(app, doc, finalX, finalY, finalWidth, finalHeight)
            };
        }, cancellationToken);

    private static JsonObject BoundingBoxMm(dynamic app, dynamic doc, double x, double y, double width, double height) =>
        new()
        {
            ["left_mm"] = ToMm(app, doc, x),
            ["bottom_mm"] = ToMm(app, doc, y),
            ["width_mm"] = ToMm(app, doc, width),
            ["height_mm"] = ToMm(app, doc, height)
        };

    private static string NormalizeHorizontal(string value) => value.Trim().ToLowerInvariant() switch
    {
        "left" => "left",
        "center" => "center",
        "right" => "right",
        _ => throw new ArgumentException("Horizontal alignment must be left, center, or right.", nameof(value))
    };

    private static string NormalizeVertical(string value) => value.Trim().ToLowerInvariant() switch
    {
        "bottom" => "bottom",
        "center" => "center",
        "top" => "top",
        _ => throw new ArgumentException("Vertical alignment must be bottom, center, or top.", nameof(value))
    };
}
