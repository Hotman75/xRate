using System.Globalization;
using System.Text.RegularExpressions;

namespace xRate.Core.Helpers;

public enum ParseResult { Incomplete, InvalidAmount, AmountOnly, Success }

public static class InputParser
{
    public static bool TryExtractAmount(string input, out double amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;
        string noSpaces = input.Replace(" ", "");
        var match = Regex.Match(noSpaces, @"-?\d+([.,]\d+)?");
        if (match.Success)
            return double.TryParse(match.Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out amount);
        return false;
    }

    public static ParseResult TryParse(string input, out double amount, out string from, out string to)
    {
        amount = 0; from = string.Empty; to = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return ParseResult.Incomplete;

        if (!TryExtractAmount(input, out amount)) return ParseResult.InvalidAmount;
        if (Math.Abs(amount) > 1_000_000_000_000) return ParseResult.InvalidAmount;

        string noSpaces = input.Replace(" ", "").ToUpperInvariant();
        string amountPart = Regex.Match(noSpaces, @"-?\d+([.,]\d+)?").Value;
        string remaining = noSpaces.Replace(amountPart, "");

        if (string.IsNullOrEmpty(remaining)) return ParseResult.AmountOnly;

        int index = 0;
        from = ExtractNextCurrency(remaining, ref index);
        if (string.IsNullOrEmpty(from)) return ParseResult.AmountOnly;

        to = ExtractNextCurrency(remaining, ref index);

        return ParseResult.Success;
    }

    private static string ExtractNextCurrency(string text, ref int currentIndex)
    {
        if (currentIndex >= text.Length) return string.Empty;
        string sub = text.Substring(currentIndex);

        if (sub.Length >= 3)
        {
            string potential = sub.Substring(0, 3);
            if (CurrencyMapper.SupportedCurrencies.Any(c => c.StartsWith(potential)))
            {
                currentIndex += 3;
                return potential;
            }
        }

        if (sub.Length >= 2)
        {
            string potential = sub.Substring(0, 2);
            if (CurrencyMapper.Normalize(potential) != potential)
            {
                currentIndex += 2;
                return potential;
            }
        }

        if (sub.Length >= 1)
        {
            string potential = sub.Substring(0, 1);
            if (CurrencyMapper.Normalize(potential) != potential)
            {
                currentIndex += 1;
                return potential;
            }
        }

        return string.Empty;
    }
}