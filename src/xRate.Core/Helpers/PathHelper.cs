namespace xRate.Core.Helpers;

public static class PathHelper
{
    public static string GetAppDataFolder()
    {
        var path = Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), ".xrate");
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        return path;
    }

    public static string GetSettingsPath() => Path.Combine(GetAppDataFolder(), "settings.json");
    public static string GetCachePath() => Path.Combine(GetAppDataFolder(), "rates_all.json");
}