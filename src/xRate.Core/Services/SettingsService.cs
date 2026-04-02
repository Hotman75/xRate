using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace xRate.Core.Services;

public class UserSettings
{
    public string DefaultFrom { get; set; } = "EUR";
    public string DefaultTo { get; set; } = "USD";
}

public class SettingsService
{
    private readonly string _settingsFilePath;
    private UserSettings? _currentSettings;

    public SettingsService()
    {
        var userProfileFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var xRateFolder = Path.Combine(userProfileFolder, ".xrate");

        Directory.CreateDirectory(xRateFolder);
        _settingsFilePath = Path.Combine(xRateFolder, "settings.json");
    }

    public UserSettings GetSettings(bool forceReload = false)
    {
        if (_currentSettings != null && !forceReload)
            return _currentSettings;

        if (File.Exists(_settingsFilePath))
        {
            try
            {
                var json = File.ReadAllText(_settingsFilePath);
                _currentSettings = JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
            }
            catch
            {
                _currentSettings = new UserSettings();
            }
        }
        else
        {
            _currentSettings = new UserSettings();
        }

        return _currentSettings;
    }

    public async Task SaveSettingsAsync(UserSettings settings)
    {
        try
        {
            _currentSettings = settings;
            var json = JsonSerializer.Serialize(_currentSettings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
        }
    }
}