using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using Windows.Networking.Connectivity;
using xRate.Core.Helpers;
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
    private int _isRefreshingFlag = 0;
    private static readonly SemaphoreSlim _fileLock = new(1, 1);
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromHours(1);

    public CurrencyService()
    {
        _cacheFilePath = PathHelper.GetCachePath();
        LoadGlobalCache();
    }

    private void LoadGlobalCache()
    {
        if (!File.Exists(_cacheFilePath))
        {
            _globalCache = new();
            return;
        }

        try
        {
            var json = File.ReadAllText(_cacheFilePath);
            _globalCache = JsonSerializer.Deserialize(json, CurrencyContext.Default.GlobalCache) ?? new();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading cache: {ex.Message}");
            _globalCache = new();
        }
    }

    private async Task SaveCacheSafelyAsync(GlobalCache cache)
    {
        await _fileLock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(cache, CurrencyContext.Default.GlobalCache);
            await File.WriteAllTextAsync(_cacheFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving cache: {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private bool IsInternetAvailable()
    {
        try
        {
            var profile = NetworkInformation.GetInternetConnectionProfile();
            return profile != null && profile.GetNetworkConnectivityLevel() == NetworkConnectivityLevel.InternetAccess;
        }
        catch { return false; }
    }

    private async Task RefreshGlobalCacheIfNeededAsync()
    {
        if (DateTime.Now - _globalCache.LastUpdate < CacheExpiry) return;
        if (Interlocked.CompareExchange(ref _isRefreshingFlag, 1, 0) == 1) return;

        try
        {
            var response = await _httpClient.GetAsync("rates");
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync(CurrencyContext.Default.RateResponseArray);

                if (data != null)
                {
                    _globalCache.Rates.Clear();
                    _globalCache.Rates["EUR"] = 1.0;
                    foreach (var item in data)
                    {
                        _globalCache.Rates[item.Quote] = item.Rate;
                    }

                    _globalCache.LastUpdate = DateTime.Now;

                    await SaveCacheSafelyAsync(_globalCache);

                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Cache refresh failed: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _isRefreshingFlag, 0);
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

        try
        {
            var url = $"rates?base={baseCurrency}&quotes={quoteCurrency}";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync(CurrencyContext.Default.RateResponseArray);

                if (data != null)
                {
                    _ = RefreshGlobalCacheIfNeededAsync();
                    return new ConversionResult { Rates = data, IsOffline = false };
                }
            }

            return CalculateFromCache(baseCurrency, quoteCurrency);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Conversion request failed: {ex.Message}");
            return CalculateFromCache(baseCurrency, quoteCurrency);
        }
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

    public ConversionResult? GetCachedConversion(string from, string to)
    {
        var baseCurrency = from.ToUpper().Trim();
        var quoteCurrency = to.ToUpper().Trim();

        if (baseCurrency == quoteCurrency)
        {
            return new ConversionResult
            {
                Rates = [new RateResponse { Rate = 1.0, Quote = quoteCurrency }],
                IsOffline = true,
                OfflineDate = DateTime.Now
            };
        }

        return CalculateFromCache(baseCurrency, quoteCurrency);
    }
}