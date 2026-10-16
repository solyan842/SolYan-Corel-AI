using System.Text.Json.Nodes;

namespace SolYan.CorelAI.Corel;

public sealed partial class CorelSession
{
    public Task<JsonObject> BatchExportPagesAsync(
        string outputDirectory,
        string baseName,
        string format,
        int startPage,
        int endPage,
        int resolutionDpi,
        bool transparent,
        int jpegQuality,
        bool includePageName,
        bool overwrite,
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
            var pageCount = Convert.ToInt32(doc.Pages.Count);
            if (startPage < 1 || startPage > pageCount)
                throw new ArgumentOutOfRangeException(nameof(startPage), $"Start page must be between 1 and {pageCount}.");
            if (endPage < startPage || endPage > pageCount)
                throw new ArgumentOutOfRangeException(nameof(endPage), $"End page must be between {startPage} and {pageCount}.");
            if (endPage - startPage + 1 > 500)
                throw new InvalidOperationException("A single batch export is limited to 500 pages.");

            var directory = Path.GetFullPath(outputDirectory ?? string.Empty);
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException($"Output directory does not exist: {directory}");

            var safeBaseName = SanitizeFileName(baseName, "page");
            var normalizedFormat = format.Trim().ToLowerInvariant() switch
            {
                "png" => "png",
                "jpeg" or "jpg" => "jpeg",
                "svg" => "svg",
                _ => throw new ArgumentException("Batch export format must be png, jpeg, or svg.", nameof(format))
            };
            if (normalizedFormat == "jpeg" && transparent)
                throw new InvalidOperationException("JPEG does not support transparency. Use PNG/SVG or set transparent=false.");

            var extension = normalizedFormat switch
            {
                "png" => ".png",
                "jpeg" => ".jpg",
                "svg" => ".svg",
                _ => throw new InvalidOperationException("Unexpected export format.")
            };

            var planned = new List<(int PageIndex, string PageName, string Path)>();
            for (var pageIndex = startPage; pageIndex <= endPage; pageIndex++)
            {
                dynamic page = doc.Pages[pageIndex];
                var rawPageName = TryString(() => page.Name);
                var pageName = string.IsNullOrWhiteSpace(rawPageName) ? $"page-{pageIndex:000}" : rawPageName!.Trim();
                var suffix = includePageName ? "-" + SanitizeFileName(pageName, $"page-{pageIndex:000}") : string.Empty;
                var fileName = $"{safeBaseName}-{pageIndex:000}{suffix}{extension}";
                planned.Add((pageIndex, pageName, Path.Combine(directory, fileName)));
            }

            if (!overwrite)
            {
                var conflicts = planned.Where(item => File.Exists(item.Path)).Select(item => item.Path).Take(10).ToArray();
                if (conflicts.Length > 0)
                    throw new IOException("Batch export would overwrite existing files. Set overwrite=true only if replacement is intended. Conflicts: " + string.Join("; ", conflicts));
            }

            var originalPageIndex = Convert.ToInt32(doc.ActivePage.Index);
            var files = new JsonArray();
            try
            {
                foreach (var item in planned)
                {
                    dynamic page = doc.Pages[item.PageIndex];
                    page.Activate();

                    if (overwrite && File.Exists(item.Path)) File.Delete(item.Path);

                    if (normalizedFormat == "svg")
                    {
                        dynamic svgFilter = doc.ExportEx(item.Path, CdrSvg, CdrCurrentPage);
                        svgFilter.Finish();
                    }
                    else
                    {
                        var filter = normalizedFormat == "png" ? CdrPng : CdrJpeg;
                        dynamic exportFilter = doc.ExportBitmap(
                            item.Path,
                            filter,
                            CdrCurrentPage,
                            CdrRgbColorImage,
                            0,
                            0,
                            resolutionDpi,
                            resolutionDpi,
                            CdrNormalAntiAliasing,
                            false,
                            transparent && normalizedFormat == "png",
                            true,
                            false);

                        if (normalizedFormat == "jpeg")
                        {
                            try
                            {
                                exportFilter.Compression = jpegQuality;
                                exportFilter.Optimized = true;
                            }
                            catch
                            {
                                // Format-specific properties differ between CorelDRAW versions.
                            }
                        }

                        exportFilter.Finish();
                    }

                    if (!File.Exists(item.Path))
                        throw new IOException($"CorelDRAW did not create the expected batch output for page {item.PageIndex}: {item.Path}");

                    files.Add(new JsonObject
                    {
                        ["page_index"] = item.PageIndex,
                        ["page_name"] = item.PageName,
                        ["path"] = item.Path,
                        ["bytes"] = new FileInfo(item.Path).Length
                    });
                }
            }
            finally
            {
                try
                {
                    if (originalPageIndex >= 1 && originalPageIndex <= Convert.ToInt32(doc.Pages.Count))
                        doc.Pages[originalPageIndex].Activate();
                    app.Refresh();
                }
                catch
                {
                    // Export result/error should not be hidden by a UI-state restore failure.
                }
            }

            return new JsonObject
            {
                ["workflow"] = "batch_export_pages",
                ["format"] = normalizedFormat,
                ["output_directory"] = directory,
                ["start_page"] = startPage,
                ["end_page"] = endPage,
                ["exported_count"] = files.Count,
                ["resolution_dpi"] = normalizedFormat == "svg" ? null : resolutionDpi,
                ["transparent"] = normalizedFormat == "png" && transparent,
                ["jpeg_quality"] = normalizedFormat == "jpeg" ? jpegQuality : null,
                ["overwrite"] = overwrite,
                ["files"] = files
            };
        }, cancellationToken);

    private static string SanitizeFileName(string? value, string fallback)
    {
        var source = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        var invalid = Path.GetInvalidFileNameChars();
        var chars = source.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray();
        var safe = new string(chars).Trim().TrimEnd('.');
        while (safe.Contains("--", StringComparison.Ordinal)) safe = safe.Replace("--", "-", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(safe) ? fallback : safe;
    }
}
