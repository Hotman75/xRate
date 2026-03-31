using System;
using System.Collections.Generic;

namespace xRate.Core.Helpers;

public static class CurrencyMapper
{
    public static readonly Dictionary<string, string> _symbolMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "€", "EUR" }, { "$", "USD" }, { "£", "GBP" }, { "¥", "JPY" },
        { "₹", "INR" }, { "₽", "RUB" }, { "₩", "KRW" }, { "₺", "TRY" },
        { "฿", "THB" }, { "₫", "VND" }, { "Rs", "INR" }, { "A$", "AUD" },
        { "C$", "CAD" }, { "zł", "PLN" }
    };

    public static readonly string[] SupportedCurrencies =
    {
        "AUD - Australian Dollar", "BGN - Bulgarian Lev", "BRL - Brazilian Real",
        "CAD - Canadian Dollar", "CHF - Swiss Franc", "CNY - Chinese Renminbi",
        "CZK - Czech Koruna", "DKK - Danish Krone", "EUR - Euro", "GBP - British Pound",
        "HKD - Hong Kong Dollar", "HUF - Hungarian Forint", "IDR - Indonesian Rupiah",
        "ILS - Israeli New Shekel", "INR - Indian Rupee", "ISK - Icelandic Krona",
        "JPY - Japanese Yen", "KRW - South Korean Won", "MXN - Mexican Peso",
        "MYR - Malaysian Ringgit", "NOK - Norwegian Krone", "NZD - New Zealand Dollar",
        "PHP - Philippine Peso", "PLN - Polish Zloty", "RON - Romanian Leu",
        "SEK - Swedish Krona", "SGD - Singapore Dollar", "THB - Thai Baht",
        "TRY - Turkish Lira", "USD - US Dollar", "ZAR - South African Rand"
    };

    public static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "EUR";

        string cleanInput = input.Trim();

        if (_symbolMap.TryGetValue(cleanInput, out var code))
        {
            return code;
        }

        if (cleanInput.Length >= 3 && cleanInput.Contains(" - "))
        {
            return cleanInput.Substring(0, 3).ToUpper();
        }

        return cleanInput.ToUpper();
    }
}