using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace xRate.Core.Helpers;

public enum ParseResult { Incomplete, InvalidAmount, AmountOnly, CurrencyOnly, Success }

public static class InputParser
{
    public static bool TryExtractAmount(string input, out double amount) => MathEvaluator.TryEvaluate(input, out amount);

    public static ParseResult TryParse(string input, out double amount, out string from, out string to)
    {
        amount = 0; from = string.Empty; to = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return ParseResult.Incomplete;

        string mathPart = Regex.Replace(input, @"[a-zA-Z\p{Sc}]+", " ").Trim();

        bool hasMath = !string.IsNullOrWhiteSpace(mathPart) && mathPart.Any(char.IsDigit);

        if (hasMath)
        {
            if (!MathEvaluator.TryEvaluate(mathPart, out amount)) return ParseResult.InvalidAmount;
            if (Math.Abs(amount) > 1_000_000_000_000) return ParseResult.InvalidAmount;
        }
        else
        {
            amount = 1;
        }

        string currencyPart = Regex.Replace(input, @"[^a-zA-Z\p{Sc}]+", " ").Trim();

        if (!string.IsNullOrWhiteSpace(currencyPart))
        {
            while (!string.IsNullOrWhiteSpace(currencyPart) && string.IsNullOrEmpty(to))
            {
                int index = 0;
                string found = ExtractNextCurrency(currencyPart, ref index);

                if (!string.IsNullOrEmpty(found))
                {
                    if (string.IsNullOrEmpty(from))
                    {
                        from = found;
                    }
                    else if (!string.Equals(found, from, StringComparison.OrdinalIgnoreCase))
                    {
                        to = found;
                    }
                    currencyPart = currencyPart.Substring(index).Trim();
                }
                else
                {
                    int nextSpace = currencyPart.IndexOf(' ');
                    if (nextSpace > 0) currencyPart = currencyPart.Substring(nextSpace).Trim();
                    else currencyPart = currencyPart.Substring(1).Trim();
                }
            }
        }

        if (string.IsNullOrEmpty(from)) return hasMath ? ParseResult.AmountOnly : ParseResult.Incomplete;

        if (!hasMath) return string.IsNullOrEmpty(to) ? ParseResult.CurrencyOnly : ParseResult.Success;

        return string.IsNullOrEmpty(to) ? ParseResult.AmountOnly : ParseResult.Success;
    }

    private static string ExtractNextCurrency(string text, ref int currentIndex)
    {
        if (string.IsNullOrEmpty(text) || currentIndex >= text.Length) return string.Empty;
        string sub = text.Substring(currentIndex).TrimStart();
        if (string.IsNullOrEmpty(sub)) return string.Empty;

        if (sub.Length >= 3)
        {
            string potential = sub.Substring(0, 3);
            if (CurrencyMapper.SupportedCurrencies.Any(c => c.StartsWith(potential, StringComparison.OrdinalIgnoreCase)))
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

                if (!normalized.Equals(potential, StringComparison.OrdinalIgnoreCase))
                {
                    currentIndex += (text.Length - sub.Length) + len;
                    return normalized;
                }
            }
        }
        return string.Empty;
    }
}