namespace SolYan.CorelAI.Host;

internal static class WindowsSettingsStore
{
    private const string DefaultModel = "gpt-6.1-sol";

    private static string SettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SolYan",
        "CorelAI");

    private static string ModelPath => Path.Combine(SettingsDirectory, "model.txt");

    public static string LoadModel()
    {
        try
        {
            if (!File.Exists(ModelPath)) return DefaultModel;
            var model = File.ReadAllText(ModelPath).Trim();
            return IsAllowedModel(model) ? model : DefaultModel;
        }
        catch
        {
            return DefaultModel;
        }
    }

    public static void SaveModel(string model)
    {
        var normalized = NormalizeModel(model);
        Directory.CreateDirectory(SettingsDirectory);
        var tempPath = ModelPath + ".tmp";
        File.WriteAllText(tempPath, normalized);
        File.Move(tempPath, ModelPath, overwrite: true);
    }

    public static string NormalizeModel(string model)
    {
        var normalized = model?.Trim();
        if (!IsAllowedModel(normalized))
            throw new ArgumentException("Unsupported model. Choose gpt-6.1-sol, gpt-6-astra, or gpt-6-luna.", nameof(model));
        return normalized!;
    }

    private static bool IsAllowedModel(string? model) => model is
        "gpt-6.1-sol" or
        "gpt-6-astra" or
        "gpt-6-luna";
}
