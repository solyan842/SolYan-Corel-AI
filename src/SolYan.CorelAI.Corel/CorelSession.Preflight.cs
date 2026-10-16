using System.Text.Json.Nodes;

namespace SolYan.CorelAI.Corel;

public sealed partial class CorelSession
{
    private const int CdrBitmapShape = 5;
    private const int CdrTextShape = 6;
    private const int CdrColorRgb = 5;
    private const int MaxDetailedPreflightIssues = 500;

    public Task<JsonObject> RunPreflightAsync(
        int minBitmapDpi = 150,
        bool flagRgb = true,
        bool checkFonts = true,
        CancellationToken cancellationToken = default) =>
        _dispatcher.InvokeAsync(() =>
        {
            if (minBitmapDpi < 36 || minBitmapDpi > 1200)
                throw new ArgumentOutOfRangeException(nameof(minBitmapDpi), "Bitmap DPI threshold must be between 36 and 1200.");

            dynamic app = App();
            if (app.Documents.Count == 0)
                return new JsonObject
                {
                    ["open"] = false,
                    ["ready_for_output"] = false,
                    ["blocking_issue_count"] = 1,
                    ["warning_count"] = 0,
                    ["issues_total"] = 1,
                    ["issues_returned"] = 1,
                    ["issues_truncated"] = false,
                    ["issues"] = new JsonArray
                    {
                        Issue("no_document", "error", "No CorelDRAW document is open.")
                    }
                };

            dynamic doc = app.ActiveDocument;
            var installedFonts = checkFonts ? ReadInstalledFonts((object)app) : null;
            var fontUsage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var fillColorTypes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var outlineColorTypes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var issues = new JsonArray();
            var missingFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var totalShapes = 0;
            var textShapes = 0;
            var bitmapShapes = 0;
            var lockedShapes = 0;
            var hiddenLayers = 0;
            var nonEditableLayers = 0;
            var lowResolutionBitmaps = 0;
            var overflowTextShapes = 0;
            var rgbFillUses = 0;
            var rgbOutlineUses = 0;

            for (var pageIndex = 1; pageIndex <= Convert.ToInt32(doc.Pages.Count); pageIndex++)
            {
                dynamic page = doc.Pages[pageIndex];

                for (var layerIndex = 1; layerIndex <= Convert.ToInt32(page.Layers.Count); layerIndex++)
                {
                    dynamic layer = page.Layers[layerIndex];
                    var layerName = TryString(() => layer.Name) ?? $"Layer {layerIndex}";
                    var visible = TryBool(() => layer.Visible);
                    var editable = TryBool(() => layer.Editable);

                    if (visible == false)
                    {
                        hiddenLayers++;
                        issues.Add(Issue(
                            "hidden_layer",
                            "info",
                            $"Layer '{layerName}' is hidden.",
                            pageIndex,
                            layer: layerName));
                    }

                    if (editable == false)
                    {
                        nonEditableLayers++;
                        issues.Add(Issue(
                            "non_editable_layer",
                            "info",
                            $"Layer '{layerName}' is not editable.",
                            pageIndex,
                            layer: layerName));
                    }
                }

                dynamic shapes = page.FindShapes();
                var shapeCount = Convert.ToInt32(shapes.Count);
                totalShapes += shapeCount;

                for (var shapeIndex = 1; shapeIndex <= shapeCount; shapeIndex++)
                {
                    dynamic shape = shapes[shapeIndex];
                    var staticId = TryInt(() => shape.StaticID);
                    var layerName = TryString(() => shape.Layer.Name);
                    var type = TryInt(() => shape.Type);

                    if (TryBool(() => shape.Locked) == true)
                    {
                        lockedShapes++;
                        issues.Add(Issue(
                            "locked_shape",
                            "info",
                            "Shape is locked.",
                            pageIndex,
                            staticId,
                            layerName));
                    }

                    if (type == CdrTextShape)
                    {
                        textShapes++;
                        var font = TryString(() => shape.Text.Story.Font);
                        if (!string.IsNullOrWhiteSpace(font))
                        {
                            fontUsage.TryGetValue(font, out var current);
                            fontUsage[font] = current + 1;

                            if (checkFonts && installedFonts is not null && !installedFonts.Contains(font))
                            {
                                if (missingFonts.Add(font))
                                {
                                    issues.Add(Issue(
                                        "missing_font",
                                        "error",
                                        $"Font '{font}' is used by the document but is not installed on this Windows system.",
                                        pageIndex,
                                        staticId,
                                        layerName));
                                }
                            }
                        }

                        if (TryBool(() => shape.Text.Overflow) == true)
                        {
                            overflowTextShapes++;
                            issues.Add(Issue(
                                "text_overflow",
                                "error",
                                "Paragraph text overflows its available text frame chain.",
                                pageIndex,
                                staticId,
                                layerName));
                        }
                    }

                    if (type == CdrBitmapShape)
                    {
                        bitmapShapes++;
                        var dpiX = TryDouble(() => shape.Bitmap.ResolutionX);
                        var dpiY = TryDouble(() => shape.Bitmap.ResolutionY);

                        if ((dpiX is not null && dpiX.Value < minBitmapDpi) ||
                            (dpiY is not null && dpiY.Value < minBitmapDpi))
                        {
                            lowResolutionBitmaps++;
                            var message = $"Bitmap resolution is {FormatDpi(dpiX)} × {FormatDpi(dpiY)} DPI; threshold is {minBitmapDpi} DPI.";
                            var issue = Issue("low_bitmap_resolution", "warning", message, pageIndex, staticId, layerName);
                            issue["resolution_x_dpi"] = dpiX;
                            issue["resolution_y_dpi"] = dpiY;
                            issue["threshold_dpi"] = minBitmapDpi;
                            issues.Add(issue);
                        }
                    }

                    var fillColorType = TryInt(() => shape.Fill.UniformColor.Type);
                    if (fillColorType is not null)
                    {
                        Increment(fillColorTypes, ColorTypeName(fillColorType.Value));
                        if (flagRgb && fillColorType == CdrColorRgb)
                        {
                            rgbFillUses++;
                            issues.Add(Issue(
                                "rgb_fill",
                                "warning",
                                "Shape uses an RGB uniform fill.",
                                pageIndex,
                                staticId,
                                layerName));
                        }
                    }

                    var outlineColorType = TryInt(() => shape.Outline.Color.Type);
                    if (outlineColorType is not null)
                    {
                        Increment(outlineColorTypes, ColorTypeName(outlineColorType.Value));
                        if (flagRgb && outlineColorType == CdrColorRgb)
                        {
                            rgbOutlineUses++;
                            issues.Add(Issue(
                                "rgb_outline",
                                "warning",
                                "Shape uses an RGB outline color.",
                                pageIndex,
                                staticId,
                                layerName));
                        }
                    }
                }
            }

            var blocking = issues.Count(node =>
                node is JsonObject item && string.Equals(item["severity"]?.GetValue<string>(), "error", StringComparison.Ordinal));
            var warnings = issues.Count(node =>
                node is JsonObject item && string.Equals(item["severity"]?.GetValue<string>(), "warning", StringComparison.Ordinal));
            var detailedIssues = LimitIssues(issues, MaxDetailedPreflightIssues);

            return new JsonObject
            {
                ["open"] = true,
                ["document"] = TryString(() => doc.Name),
                ["page_count"] = Convert.ToInt32(doc.Pages.Count),
                ["ready_for_output"] = blocking == 0,
                ["blocking_issue_count"] = blocking,
                ["warning_count"] = warnings,
                ["issues_total"] = issues.Count,
                ["issues_returned"] = detailedIssues.Count,
                ["issues_truncated"] = issues.Count > detailedIssues.Count,
                ["settings"] = new JsonObject
                {
                    ["min_bitmap_dpi"] = minBitmapDpi,
                    ["flag_rgb"] = flagRgb,
                    ["check_fonts"] = checkFonts,
                    ["max_detailed_issues"] = MaxDetailedPreflightIssues
                },
                ["summary"] = new JsonObject
                {
                    ["total_shapes"] = totalShapes,
                    ["text_shapes"] = textShapes,
                    ["bitmap_shapes"] = bitmapShapes,
                    ["locked_shapes"] = lockedShapes,
                    ["hidden_layers"] = hiddenLayers,
                    ["non_editable_layers"] = nonEditableLayers,
                    ["low_resolution_bitmaps"] = lowResolutionBitmaps,
                    ["overflow_text_shapes"] = overflowTextShapes,
                    ["rgb_fill_uses"] = rgbFillUses,
                    ["rgb_outline_uses"] = rgbOutlineUses
                },
                ["font_usage"] = DictionaryToJson(fontUsage),
                ["missing_fonts"] = StringsToJson(missingFonts.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)),
                ["fill_color_types"] = DictionaryToJson(fillColorTypes),
                ["outline_color_types"] = DictionaryToJson(outlineColorTypes),
                ["issues"] = detailedIssues
            };
        }, cancellationToken);

    private static HashSet<string> ReadInstalledFonts(object appObject)
    {
        dynamic app = appObject;
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            dynamic fonts = app.FontList;
            for (var i = 1; i <= Convert.ToInt32(fonts.Count); i++)
            {
                var name = Convert.ToString(fonts[i]);
                if (!string.IsNullOrWhiteSpace(name)) result.Add(name.Trim());
            }
        }
        catch
        {
            // Font availability remains best-effort on older Corel runtimes.
        }
        return result;
    }

    private static JsonObject Issue(
        string code,
        string severity,
        string message,
        int? page = null,
        int? staticId = null,
        string? layer = null)
    {
        var result = new JsonObject
        {
            ["code"] = code,
            ["severity"] = severity,
            ["message"] = message
        };
        if (page is not null) result["page"] = page.Value;
        if (staticId is not null) result["static_id"] = staticId.Value;
        if (!string.IsNullOrWhiteSpace(layer)) result["layer"] = layer;
        return result;
    }

    private static JsonArray LimitIssues(JsonArray source, int limit)
    {
        if (source.Count <= limit) return source;

        var result = new JsonArray();
        for (var i = 0; i < limit; i++)
        {
            var node = source[i];
            if (node is not null) result.Add(node.DeepClone());
        }
        return result;
    }

    private static void Increment(Dictionary<string, int> values, string key)
    {
        values.TryGetValue(key, out var current);
        values[key] = current + 1;
    }

    private static JsonObject DictionaryToJson(Dictionary<string, int> values)
    {
        var result = new JsonObject();
        foreach (var pair in values.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            result[pair.Key] = pair.Value;
        return result;
    }

    private static JsonArray StringsToJson(IEnumerable<string> values)
    {
        var result = new JsonArray();
        foreach (var value in values) result.Add(value);
        return result;
    }

    private static string FormatDpi(double? value) =>
        value is null ? "unknown" : Math.Round(value.Value).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string ColorTypeName(int type) => type switch
    {
        1 => "pantone",
        2 => "cmyk",
        4 => "cmy",
        5 => "rgb",
        6 => "hsb",
        7 => "hls",
        8 => "black_and_white",
        9 => "gray",
        11 => "yiq",
        12 => "lab",
        14 => "pantone_hex",
        20 => "registration",
        22 => "user_ink",
        25 => "spot",
        26 => "multi_channel",
        99 => "mixed",
        _ => $"type_{type}"
    };
}
