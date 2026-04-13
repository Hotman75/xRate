using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace xRate.Core.Helpers;

public enum ParseResult { Incomplete, InvalidAmount, AmountOnly, Success }

public static class InputParser
{
    public static bool TryExtractAmount(string input, out double amount)
    {
        return TryEvaluate(input, out amount);
    }

    public static ParseResult TryParse(string input, out double amount, out string from, out string to)
    {
        amount = 0;
        from = string.Empty;
        to = string.Empty;

        if (string.IsNullOrWhiteSpace(input)) return ParseResult.Incomplete;

        var match = Regex.Match(input, @"[a-zA-Z\p{Sc}]");
        string mathPart = match.Success ? input.Substring(0, match.Index).Trim() : input.Trim();
        string currencyPart = match.Success ? input.Substring(match.Index).Trim() : string.Empty;

        if (!TryEvaluate(mathPart, out amount)) return ParseResult.InvalidAmount;
        if (Math.Abs(amount) > 1_000_000_000_000) return ParseResult.InvalidAmount;

        if (string.IsNullOrEmpty(currencyPart)) return ParseResult.AmountOnly;

        int index = 0;
        from = ExtractNextCurrency(currencyPart, ref index);
        if (string.IsNullOrEmpty(from)) return ParseResult.AmountOnly;

        string remaining = currencyPart.Substring(index).Trim();
        index = 0;
        to = ExtractNextCurrency(remaining, ref index);

        return string.IsNullOrEmpty(to) ? ParseResult.AmountOnly : ParseResult.Success;
    }

    private static bool TryEvaluate(string expression, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(expression)) return false;

        try
        {
            var cleanMatch = Regex.Match(expression, @"^[0-9\s\+\-\*\/\(\)\,\.]*");
            string sanitized = cleanMatch.Value.Replace(",", ".");

            if (string.IsNullOrWhiteSpace(sanitized)) return false;

            using var dt = new DataTable();
            var val = dt.Compute(sanitized, "");
            result = Convert.ToDouble(val, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return double.TryParse(expression.Split(' ')[0].Replace(',', '.'),
                NumberStyles.Any, CultureInfo.InvariantCulture, out result);
        }
    }

    private static string ExtractNextCurrency(string text, ref int currentIndex)
    {
        if (string.IsNullOrEmpty(text) || currentIndex >= text.Length) return string.Empty;
        string sub = text.Substring(currentIndex).TrimStart();
        if (string.IsNullOrEmpty(sub)) return string.Empty;

        if (sub.Length >= 3)
        {
            string potential = sub.Substring(0, 3);
            if (CurrencyMapper.SupportedCurrencies.Any(c => c.Equals(potential, StringComparison.OrdinalIgnoreCase)))
            {
                currentIndex += (text.Length - sub.Length) + 3;
                return potential.ToUpperInvariant();
            }
        }

        for (int len = 2; len >= 1; len--)
        {
            if (sub.Length >= len)
            {
                string potential = sub.Substring(0, len);
                string normalized = CurrencyMapper.Normalize(potential);
                if (normalized != potential || CurrencyMapper.SupportedCurrencies.Contains(normalized.ToUpperInvariant()))
                {
                    currentIndex += (text.Length - sub.Length) + len;
                    return normalized.ToUpperInvariant();
                }
            }
        }
        return string.Empty;
    }
}