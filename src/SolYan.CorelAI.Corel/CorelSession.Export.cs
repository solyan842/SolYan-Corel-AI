using System.Text.Json.Nodes;

namespace SolYan.CorelAI.Corel;

public sealed partial class CorelSession
{
    private const int CdrJpeg = 774;
    private const int CdrSvg = 1345;
    private const int CdrSelection = 2;
    private const int CdrNormalAntiAliasing = 1;

    public Task<JsonObject> ExportRasterAsync(
        string path,
        string format,
        string range,
        int resolutionDpi,
        bool transparent,
        int jpegQuality,
        CancellationToken cancellationToken = default) =>
        _dispatcher.InvokeAsync(() =>
        {
            if (resolutionDpi < 48 || resolutionDpi > 1200)
                throw new ArgumentOutOfRangeException(nameof(resolutionDpi), "Resolution must be between 48 and 1200 DPI.");
            if (jpegQuality < 1 || jpegQuality > 100)
                throw new ArgumentOutOfRangeException(nameof(jpegQuality), "JPEG quality must be between 1 and 100.");

            dynamic app = App();
            if (app.Documents.Count == 0) throw new InvalidOperationException("No CorelDRAW document is open.");
            dynamic doc = app.ActiveDocument;

            var normalizedFormat = format.Trim().ToLowerInvariant();
            var filter = normalizedFormat switch
            {
                "png" => CdrPng,
                "jpeg" or "jpg" => CdrJpeg,
                _ => throw new ArgumentException("Raster format must be png or jpeg.", nameof(format))
            };
            var exportRange = ResolveExportRange(doc, range);
            if (filter == CdrJpeg && transparent)
                throw new InvalidOperationException("JPEG does not support transparent export. Use PNG or set transparent=false.");

            var fullPath = RequireWritableTarget(path);
            ValidateExportExtension(fullPath, normalizedFormat == "png" ? new[] { ".png" } : new[] { ".jpg", ".jpeg" });

            dynamic exportFilter = doc.ExportBitmap(
                fullPath,
                filter,
                exportRange,
                CdrRgbColorImage,
                0,
                0,
                resolutionDpi,
                resolutionDpi,
                CdrNormalAntiAliasing,
                false,
                transparent,
                true,
                false);

            if (filter == CdrJpeg)
            {
                try
                {
                    exportFilter.Compression = jpegQuality;
                    exportFilter.Optimized = true;
                }
                catch
                {
                    // Corel versions can expose format-specific filter properties differently.
                }
            }

            exportFilter.Finish();
            if (!File.Exists(fullPath)) throw new IOException("CorelDRAW completed export but the output file was not created.");

            return new JsonObject
            {
                ["path"] = fullPath,
                ["format"] = normalizedFormat == "jpg" ? "jpeg" : normalizedFormat,
                ["range"] = range.Trim().ToLowerInvariant(),
                ["resolution_dpi"] = resolutionDpi,
                ["transparent"] = transparent,
                ["bytes"] = new FileInfo(fullPath).Length
            };
        }, cancellationToken);

    public Task<JsonObject> ExportSvgAsync(
        string path,
        string range,
        CancellationToken cancellationToken = default) =>
        _dispatcher.InvokeAsync(() =>
        {
            dynamic app = App();
            if (app.Documents.Count == 0) throw new InvalidOperationException("No CorelDRAW document is open.");
            dynamic doc = app.ActiveDocument;
            var exportRange = ResolveExportRange(doc, range);
            var fullPath = RequireWritableTarget(path);
            ValidateExportExtension(fullPath, new[] { ".svg" });

            dynamic exportFilter = doc.ExportEx(fullPath, CdrSvg, exportRange);
            exportFilter.Finish();
            if (!File.Exists(fullPath)) throw new IOException("CorelDRAW completed SVG export but the output file was not created.");

            return new JsonObject
            {
                ["path"] = fullPath,
                ["format"] = "svg",
                ["range"] = range.Trim().ToLowerInvariant(),
                ["bytes"] = new FileInfo(fullPath).Length
            };
        }, cancellationToken);

    private static int ResolveExportRange(dynamic doc, string range)
    {
        return range.Trim().ToLowerInvariant() switch
        {
            "page" or "current_page" => CdrCurrentPage,
            "selection" when Convert.ToInt32(doc.ActiveSelectionRange.Count) > 0 => CdrSelection,
            "selection" => throw new InvalidOperationException("Nothing is selected for selection export."),
            _ => throw new ArgumentException("Export range must be page or selection.", nameof(range))
        };
    }

    private static void ValidateExportExtension(string path, IReadOnlyCollection<string> allowedExtensions)
    {
        var extension = Path.GetExtension(path);
        if (!allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Output path must use one of these extensions: {string.Join(", ", allowedExtensions)}", nameof(path));
    }
}
