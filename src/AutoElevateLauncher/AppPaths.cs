namespace AutoElevateLauncher;

public static class AppPaths
{
    public static string AppDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AutoElevateLauncher");

    public static string ConfigFile => Path.Combine(AppDataDirectory, "config.json");

    public static string LogsDirectory => Path.Combine(AppDataDirectory, "logs");

    public static string GetItemLogDirectory(string itemId) => Path.Combine(LogsDirectory, itemId);
}