using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using xRate.Core.Models;

namespace xRate.Core.Services;

public class CurrencyService
{
    private static readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri("https://api.frankfurter.dev/v2/"),
        Timeout = TimeSpan.FromSeconds(8)
    };

    public async Task<RateResponse[]?> GetConversionAsync(string from, string to)
    {
        var baseCurrency = from.ToUpper().Trim();
        var quoteCurrency = to.ToUpper().Trim();

        if (baseCurrency == quoteCurrency)
        {
            return new[] { new RateResponse { Rate = 1.0, Quote = quoteCurrency } };
        }

        try
        {
            var url = $"rates?base={baseCurrency}&quotes={quoteCurrency}";
            var typeInfo = (JsonTypeInfo<RateResponse[]>)CurrencyContext.Default.GetTypeInfo(typeof(RateResponse[]));

            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync(typeInfo);
            }

            System.Diagnostics.Debug.WriteLine($"xRate API Warning: {response.StatusCode}");
            return null;
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"xRate Network Error: {ex.Message}");
            return null;
        }
        catch (TaskCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("xRate API Timeout reached.");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"xRate Unexpected Error: {ex.Message}");
            return null;
        }
    }
}