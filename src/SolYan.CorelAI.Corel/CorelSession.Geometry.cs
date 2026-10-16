namespace SolYan.CorelAI.Corel;

public sealed partial class CorelSession
{
    public Task MoveSelectionToAsync(double leftMm, double bottomMm, CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            dynamic app = App();
            dynamic doc = app.ActiveDocument;
            dynamic range = app.ActiveSelectionRange;
            EnsureSelection(range);

            double x = 0, y = 0, w = 0, h = 0;
            range.GetBoundingBox(ref x, ref y, ref w, ref h);
            var targetX = ToDocumentUnits(app, doc, leftMm);
            var targetY = ToDocumentUnits(app, doc, bottomMm);
            range.Move(targetX - x, targetY - y);
        }, cancellationToken);

    public Task RotateSelectionAsync(double angleDegrees, CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            dynamic range = App().ActiveSelectionRange;
            EnsureSelection(range);
            range.Rotate(angleDegrees);
        }, cancellationToken);

    public Task OrderSelectionAsync(string order, CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            dynamic range = App().ActiveSelectionRange;
            EnsureSelection(range);

            switch (order.Trim().ToLowerInvariant())
            {
                case "front":
                    range.OrderToFront();
                    break;
                case "back":
                    range.OrderToBack();
                    break;
                default:
                    throw new ArgumentException("Order must be 'front' or 'back'.", nameof(order));
            }
        }, cancellationToken);

    public Task AlignSelectionAsync(string mode, string target, CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            dynamic app = App();
            dynamic doc = app.ActiveDocument;
            dynamic range = app.ActiveSelectionRange;
            EnsureSelection(range);

            double rangeX = 0, rangeY = 0, rangeW = 0, rangeH = 0;
            range.GetBoundingBox(ref rangeX, ref rangeY, ref rangeW, ref rangeH);

            var normalizedTarget = target.Trim().ToLowerInvariant();
            double targetLeft;
            double targetBottom;
            double targetRight;
            double targetTop;

            if (normalizedTarget == "selection")
            {
                targetLeft = rangeX;
                targetBottom = rangeY;
                targetRight = rangeX + rangeW;
                targetTop = rangeY + rangeH;
            }
            else if (normalizedTarget == "page")
            {
                var pageWidth = Convert.ToDouble(doc.ActivePage.SizeWidth);
                var pageHeight = Convert.ToDouble(doc.ActivePage.SizeHeight);
                var pageCenterX = -Convert.ToDouble(doc.DrawingOriginX);
                var pageCenterY = -Convert.ToDouble(doc.DrawingOriginY);
                targetLeft = pageCenterX - pageWidth / 2.0;
                targetRight = pageCenterX + pageWidth / 2.0;
                targetBottom = pageCenterY - pageHeight / 2.0;
                targetTop = pageCenterY + pageHeight / 2.0;
            }
            else
            {
                throw new ArgumentException("Target must be 'page' or 'selection'.", nameof(target));
            }

            var normalizedMode = mode.Trim().ToLowerInvariant();
            for (var i = 1; i <= Convert.ToInt32(range.Count); i++)
            {
                dynamic shape = range[i];
                double x = 0, y = 0, w = 0, h = 0;
                shape.GetBoundingBox(ref x, ref y, ref w, ref h);

                var dx = 0d;
                var dy = 0d;
                switch (normalizedMode)
                {
                    case "left":
                        dx = targetLeft - x;
                        break;
                    case "center_x":
                        dx = ((targetLeft + targetRight) / 2.0) - (x + w / 2.0);
                        break;
                    case "right":
                        dx = targetRight - (x + w);
                        break;
                    case "bottom":
                        dy = targetBottom - y;
                        break;
                    case "center_y":
                        dy = ((targetBottom + targetTop) / 2.0) - (y + h / 2.0);
                        break;
                    case "top":
                        dy = targetTop - (y + h);
                        break;
                    default:
                        throw new ArgumentException("Mode must be left, center_x, right, bottom, center_y, or top.", nameof(mode));
                }

                shape.Move(dx, dy);
            }
        }, cancellationToken);

    public Task DistributeSelectionAsync(string axis, CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            dynamic range = App().ActiveSelectionRange;
            EnsureSelection(range);
            var count = Convert.ToInt32(range.Count);
            if (count < 3) throw new InvalidOperationException("At least three selected objects are required for distribution.");

            var items = new List<(object Shape, double X, double Y, double W, double H)>();
            for (var i = 1; i <= count; i++)
            {
                dynamic shape = range[i];
                double x = 0, y = 0, w = 0, h = 0;
                shape.GetBoundingBox(ref x, ref y, ref w, ref h);
                items.Add(((object)shape, x, y, w, h));
            }

            var normalizedAxis = axis.Trim().ToLowerInvariant();
            if (normalizedAxis == "horizontal")
            {
                items.Sort((a, b) => a.X.CompareTo(b.X));
                var left = items[0].X;
                var right = items[^1].X + items[^1].W;
                var occupied = items.Sum(item => item.W);
                var gap = (right - left - occupied) / (count - 1);
                var cursor = left;

                foreach (var item in items)
                {
                    dynamic shape = item.Shape;
                    shape.Move(cursor - item.X, 0d);
                    cursor += item.W + gap;
                }
            }
            else if (normalizedAxis == "vertical")
            {
                items.Sort((a, b) => a.Y.CompareTo(b.Y));
                var bottom = items[0].Y;
                var top = items[^1].Y + items[^1].H;
                var occupied = items.Sum(item => item.H);
                var gap = (top - bottom - occupied) / (count - 1);
                var cursor = bottom;

                foreach (var item in items)
                {
                    dynamic shape = item.Shape;
                    shape.Move(0d, cursor - item.Y);
                    cursor += item.H + gap;
                }
            }
            else
            {
                throw new ArgumentException("Axis must be 'horizontal' or 'vertical'.", nameof(axis));
            }
        }, cancellationToken);

    private static void EnsureSelection(dynamic range)
    {
        if (Convert.ToInt32(range.Count) == 0)
            throw new InvalidOperationException("Nothing is selected.");
    }
}
