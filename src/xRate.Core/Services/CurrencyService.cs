using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using xRate.Core.Models; // Import des modèles

namespace xRate.Core.Services;

public class CurrencyService
{
    private static readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri("https://api.frankfurter.dev/v2/")
    };

    public async Task<RateResponse[]?> GetConversionAsync(string from, string to)
    {
        try
        {
            var baseCurrency = from.ToUpper().Trim();
            var quoteCurrency = to.ToUpper().Trim();

            var url = $"rates?base={baseCurrency}&quotes={quoteCurrency}";

            var typeInfo = (JsonTypeInfo<RateResponse[]>)CurrencyContext.Default.GetTypeInfo(typeof(RateResponse[]));

            return await _httpClient.GetFromJsonAsync(url, typeInfo);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"xRate API Error: {ex.Message}");
            return null;
        }
    }
}