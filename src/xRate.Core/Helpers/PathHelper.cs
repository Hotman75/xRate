using Windows.Storage;

namespace xRate.Core.Helpers;

public static class PathHelper
{
    private static string? _appDataFolder;

    public static void Initialize()
    {
        if (_appDataFolder != null) return;

        try
        {
            StorageFolder folder = ApplicationData.Current.GetPublisherCacheFolder("xrate");
            _appDataFolder = folder.Path;
        }
        catch
        {
            _appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "xrate"
            );
        }

        Directory.CreateDirectory(_appDataFolder!);
    }

    public static string GetAppDataFolder() =>
        _appDataFolder ?? throw new InvalidOperationException(
            "PathHelper.Initialize() must be called at startup.");

    public static string GetSettingsPath() => Path.Combine(GetAppDataFolder(), "settings.json");
    public static string GetCachePath() => Path.Combine(GetAppDataFolder(), "rates_all.json");
}