namespace SolYan.CorelAI.Corel;

public sealed partial class CorelSession
{
    private const int CdrPng = 802;
    private const int CdrCurrentPage = 1;
    private const int CdrRgbColorImage = 4;

    public Task<byte[]> RenderPagePreviewPngAsync(
        int resolutionDpi = 110,
        CancellationToken cancellationToken = default) =>
        _dispatcher.InvokeAsync(() =>
        {
            if (resolutionDpi < 48 || resolutionDpi > 300)
                throw new ArgumentOutOfRangeException(nameof(resolutionDpi), "Preview DPI must be between 48 and 300.");

            dynamic app = App();
            if (app.Documents.Count == 0)
                throw new InvalidOperationException("No CorelDRAW document is open.");

            dynamic doc = app.ActiveDocument;
            var tempPath = Path.Combine(Path.GetTempPath(), $"solyan-corel-preview-{Guid.NewGuid():N}.png");

            try
            {
                dynamic exportFilter = doc.ExportBitmap(
                    tempPath,
                    CdrPng,
                    CdrCurrentPage,
                    CdrRgbColorImage,
                    0,
                    0,
                    resolutionDpi,
                    resolutionDpi);

                exportFilter.Finish();

                if (!File.Exists(tempPath))
                    throw new IOException("CorelDRAW completed the preview export but no PNG file was produced.");

                return File.ReadAllBytes(tempPath);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
                catch
                {
                    // A temporary preview cleanup failure must not invalidate the AI task.
                }
            }
        }, cancellationToken);
}
