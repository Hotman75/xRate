using System;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;

namespace xRate.Core.Helpers;

public static class MathEvaluator
{
    public static bool TryEvaluate(string expression, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(expression)) return false;

        try
        {
            var cleanMatch = Regex.Match(expression, @"^[0-9\s\+\-\*\/\(\)\,\.\%]*");
            string sanitized = cleanMatch.Value;

            if (string.IsNullOrWhiteSpace(sanitized)) return false;

            sanitized = NormalizeNumbers(sanitized);

            sanitized = Regex.Replace(sanitized, @"(\d+(?:\.\d+)?)\s*([\+\-])\s*(\d+(?:\.\d+)?)\s*%", "$1 $2 ($1 * $3 / 100)");

            sanitized = Regex.Replace(sanitized, @"(\d+(?:\.\d+)?)\s*%", "($1 / 100)");

            int openParen = sanitized.Count(c => c == '(');
            int closeParen = sanitized.Count(c => c == ')');
            if (openParen > closeParen)
                sanitized += new string(')', openParen - closeParen);

            using var dt = new DataTable();
            var val = dt.Compute(sanitized, "");
            result = Convert.ToDouble(val, CultureInfo.InvariantCulture);

            if (double.IsInfinity(result) || double.IsNaN(result))
                throw new DivideByZeroException();

            return true;
        }
        catch
        {
            var firstNumMatch = Regex.Match(expression, @"-?\d+([.,]\d+)?");
            if (firstNumMatch.Success)
            {
                return double.TryParse(firstNumMatch.Value.Replace(',', '.'),
                    NumberStyles.Any, CultureInfo.InvariantCulture, out result);
            }
            return false;
        }
    }

    private static string NormalizeNumbers(string input)
    {
        return Regex.Replace(input, @"[\d\,\.\s]+", match =>
        {
            string num = match.Value.Trim().Replace(" ", "");
            if (string.IsNullOrEmpty(num)) return "";

            int lastComma = num.LastIndexOf(',');
            int lastDot = num.LastIndexOf('.');

            if (lastComma > -1 && lastDot > -1)
            {
                return (lastComma > lastDot) ? num.Replace(".", "").Replace(",", ".") : num.Replace(",", "");
            }
            if (lastComma > -1)
            {
                return (num.IndexOf(',') != lastComma) ? num.Replace(",", "") : num.Replace(",", ".");
            }
            if (lastDot > -1 && num.IndexOf('.') != lastDot) return num.Replace(".", "");

            return num;
        });
    }
}