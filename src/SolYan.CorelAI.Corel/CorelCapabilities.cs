using System.Text.Json.Nodes;

namespace SolYan.CorelAI.Corel;

public sealed record CorelCapabilities(
    string? Version,
    string? Build,
    bool HasDocumentToUnits,
    bool HasApplicationConvertUnits,
    bool HasCommandGroups,
    bool HasPdfPublish,
    bool HasActiveSelectionRange,
    bool HasCreateArtisticText,
    bool HasCreateRectangle2)
{
    public JsonObject ToJson() => new()
    {
        ["version"] = Version,
        ["build"] = Build,
        ["document_to_units"] = HasDocumentToUnits,
        ["application_convert_units"] = HasApplicationConvertUnits,
        ["command_groups"] = HasCommandGroups,
        ["publish_pdf"] = HasPdfPublish,
        ["active_selection_range"] = HasActiveSelectionRange,
        ["create_artistic_text"] = HasCreateArtisticText,
        ["create_rectangle2"] = HasCreateRectangle2,
        ["ai_geometry_unit"] = "mm"
    };
}
