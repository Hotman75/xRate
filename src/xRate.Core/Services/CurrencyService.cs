using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Windows.Networking.Connectivity;
using xRate.Core.Models;

namespace xRate.Core.Services;

public class GlobalCache
{
    public DateTime LastUpdate { get; set; }
    public Dictionary<string, double> Rates { get; set; } = new();
}

public class CurrencyService
{
    private static readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri("https://api.frankfurter.dev/v2/"),
        Timeout = TimeSpan.FromSeconds(8)
    };

    private readonly string _cacheFilePath;
    private GlobalCache _globalCache = new();

    public CurrencyService()
    {
        var userProfileFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var xRateFolder = Path.Combine(userProfileFolder, ".xrate");

        Directory.CreateDirectory(xRateFolder);

        _cacheFilePath = Path.Combine(xRateFolder, "rates_all.json");

        LoadGlobalCache();
    }

    private void LoadGlobalCache()
    {
        if (File.Exists(_cacheFilePath))
        {
            try
            {
                var json = File.ReadAllText(_cacheFilePath);
                _globalCache = JsonSerializer.Deserialize<GlobalCache>(json) ?? new();
            }
            catch { _globalCache = new(); }
        }
    }

    private bool IsInternetAvailable()
    {
        var profile = NetworkInformation.GetInternetConnectionProfile();
        if (profile == null) return false;

        var level = profile.GetNetworkConnectivityLevel();
        return level == NetworkConnectivityLevel.InternetAccess;
    }

    private async Task RefreshGlobalCacheIfNeededAsync()
    {
        if (DateTime.Now - _globalCache.LastUpdate < TimeSpan.FromHours(12)) return;

        try
        {
            var response = await _httpClient.GetAsync("rates");
            if (response.IsSuccessStatusCode)
            {
                var typeInfo = (JsonTypeInfo<RateResponse[]>)CurrencyContext.Default.GetTypeInfo(typeof(RateResponse[]));
                var data = await response.Content.ReadFromJsonAsync(typeInfo);

                if (data != null)
                {
                    _globalCache.Rates.Clear();
                    _globalCache.Rates["EUR"] = 1.0;
                    foreach (var item in data)
                    {
                        _globalCache.Rates[item.Quote] = item.Rate;
                    }
                    _globalCache.LastUpdate = DateTime.Now;

                    var json = JsonSerializer.Serialize(_globalCache);
                    await File.WriteAllTextAsync(_cacheFilePath, json);
                }
            }
        }
        catch
        {
        }
    }

    public async Task<ConversionResult?> GetConversionAsync(string from, string to)
    {
        var baseCurrency = from.ToUpper().Trim();
        var quoteCurrency = to.ToUpper().Trim();

        if (baseCurrency == quoteCurrency)
        {
            return new ConversionResult
            {
                Rates = [new RateResponse { Rate = 1.0, Quote = quoteCurrency }],
                IsOffline = false
            };
        }

        if (!IsInternetAvailable())
        {
            return CalculateFromCache(baseCurrency, quoteCurrency);
        }

        _ = RefreshGlobalCacheIfNeededAsync();

        try
        {
            var url = $"rates?base={baseCurrency}&quotes={quoteCurrency}";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var typeInfo = (JsonTypeInfo<RateResponse[]>)CurrencyContext.Default.GetTypeInfo(typeof(RateResponse[]));
                var data = await response.Content.ReadFromJsonAsync(typeInfo);

                if (data != null)
                    return new ConversionResult { Rates = data, IsOffline = false };
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                System.Diagnostics.Debug.WriteLine("Rate limited (429) by Frankfurter API. Falling back to cache.");
                return CalculateFromCache(baseCurrency, quoteCurrency);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"API Error ({response.StatusCode}). Falling back to cache.");
                return CalculateFromCache(baseCurrency, quoteCurrency);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Network Error: {ex.Message}");
            return CalculateFromCache(baseCurrency, quoteCurrency);
        }

        return CalculateFromCache(baseCurrency, quoteCurrency);
    }

    private ConversionResult? CalculateFromCache(string from, string to)
    {
        if (_globalCache.Rates.TryGetValue(from, out double rateFrom) &&
            _globalCache.Rates.TryGetValue(to, out double rateTo))
        {
            double crossRate = rateTo / rateFrom;

            return new ConversionResult
            {
                Rates = [new RateResponse { Quote = to, Rate = crossRate }],
                IsOffline = true,
                OfflineDate = _globalCache.LastUpdate
            };
        }
        return null;
    }
}