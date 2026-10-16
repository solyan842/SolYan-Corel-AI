namespace SolYan.CorelAI.Corel;

public sealed partial class CorelSession
{
    public Task SetFillCmykAsync(int c, int m, int y, int k, CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            dynamic app = App();
            dynamic color = app.CreateCMYKColor(ClampPercent(c), ClampPercent(m), ClampPercent(y), ClampPercent(k));
            dynamic range = app.ActiveSelectionRange;
            EnsureSelection(range);
            for (var i = 1; i <= Convert.ToInt32(range.Count); i++)
                range[i].Fill.ApplyUniformFill(color);
        }, cancellationToken);

    public Task SetOutlineCmykAsync(int c, int m, int y, int k, double widthMm, CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            if (widthMm < 0) throw new ArgumentOutOfRangeException(nameof(widthMm));

            dynamic app = App();
            dynamic doc = app.ActiveDocument;
            dynamic color = app.CreateCMYKColor(ClampPercent(c), ClampPercent(m), ClampPercent(y), ClampPercent(k));
            dynamic range = app.ActiveSelectionRange;
            EnsureSelection(range);
            var width = ToDocumentUnits(app, doc, widthMm);

            for (var i = 1; i <= Convert.ToInt32(range.Count); i++)
            {
                range[i].Outline.Color = color;
                range[i].Outline.Width = width;
            }
        }, cancellationToken);

    public Task SetSelectedTypographyAsync(
        string font,
        double sizePt,
        bool bold,
        bool italic,
        CancellationToken cancellationToken = default) =>
        MutateAsync(() =>
        {
            var fontName = RequireName(font, nameof(font));
            if (!double.IsFinite(sizePt) || sizePt <= 0 || sizePt > 10000)
                throw new ArgumentOutOfRangeException(nameof(sizePt), "Font size must be a positive point value.");

            dynamic range = App().ActiveSelectionRange;
            EnsureSelection(range);
            var changed = 0;

            for (var i = 1; i <= Convert.ToInt32(range.Count); i++)
            {
                try
                {
                    dynamic story = range[i].Text.Story;
                    story.Font = fontName;
                    story.Size = sizePt;
                    story.Bold = bold;
                    story.Italic = italic;
                    changed++;
                }
                catch
                {
                    // Skip shapes that do not expose an editable text story.
                }
            }

            if (changed == 0)
                throw new InvalidOperationException("The current selection contains no editable text shape.");
        }, cancellationToken);

    private static int ClampPercent(int value) => Math.Clamp(value, 0, 100);
}
