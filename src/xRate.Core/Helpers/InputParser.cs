using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace xRate.Core.Helpers;

public enum ParseResult
{
    Incomplete,
    InvalidAmount,
    Success
}

public static class InputParser
{
    // Matches patterns like: "100 EUR USD", "100EUR USD", "100.50 € $", "100eur usd"
    // Group 1: Amount (\d+(?:[.,]\d+)?)
    // Group 2: From currency ([^\d\s]+)
    // Group 3: To currency ([^\d\s]+)
    private static readonly Regex InputRegex = new Regex(
        @"^(\d+(?:[.,]\d+)?)\s*([^\d\s]+)?\s*([^\d\s]+)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static ParseResult TryParse(string input, out double amount, out string from, out string to)
    {
        amount = 0;
        from = string.Empty;
        to = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return ParseResult.Incomplete;
        }

        string cleanedInput = input.Trim().ToLowerInvariant()
            .Replace(" to ", " ")
            .Replace(" in ", " ")
            .Replace(" - ", " ");

        var match = InputRegex.Match(cleanedInput);

        if (!match.Success)
        {
            return ParseResult.InvalidAmount;
        }

        string amountStr = match.Groups[1].Value.Replace(',', '.');

        if (!double.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out amount))
        {
            return ParseResult.InvalidAmount;
        }

        from = match.Groups[2].Success ? CurrencyMapper.Normalize(match.Groups[2].Value) : string.Empty;
        to = match.Groups[3].Success ? CurrencyMapper.Normalize(match.Groups[3].Value) : string.Empty;

        if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
        {
            return ParseResult.Incomplete;
        }

        return ParseResult.Success;
    }
}