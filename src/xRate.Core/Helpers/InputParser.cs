using System.Globalization;
using System.Text.RegularExpressions;

namespace xRate.Core.Helpers;

public enum ParseResult
{
    Incomplete,
    InvalidAmount,
    AmountOnly,
    Success
}

public static class InputParser
{
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

        if (!match.Groups[2].Success && !match.Groups[3].Success)
        {
            return ParseResult.AmountOnly;
        }

        from = match.Groups[2].Success ? CurrencyMapper.Normalize(match.Groups[2].Value) : string.Empty;

        if (!match.Groups[3].Success)
        {
            return ParseResult.Incomplete;
        }

        to = CurrencyMapper.Normalize(match.Groups[3].Value);

        return ParseResult.Success;
    }
}