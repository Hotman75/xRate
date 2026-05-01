using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using xRate.Core.Helpers;
using xRate.Core.Models;
using xRate.Core.Services;
using xRateExt.Helpers;

namespace xRateExt.Pages;

internal sealed partial class xRateExtPage : DynamicListPage
{
    private readonly List<ListItem> _items = [];
    private readonly CurrencyService _apiService = new();
    private readonly SettingsService _settingsService = new();
    private UserSettings _settings;
    private CancellationTokenSource? _debounceTimer;

    private readonly LaunchAppCommand _launchCommand = new();
    private readonly ListCurrenciesPage _currenciesPage = new();
    private readonly SettingsPage _settingsPage = new();


    public xRateExtPage()
    {
        this.Name = "xRate";
        this.Icon = IconHelpers.FromRelativePath("Assets\\icon.png");
        this.PlaceholderText = "Amount <From> <To> (e.g. 100 USD EUR)";

        _settings = _settingsService.GetSettings(true);
        UpdateDisplay(string.Empty);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _settings = _settingsService.GetSettings(true);

        if (string.IsNullOrWhiteSpace(newSearch))
        {
            _debounceTimer?.Cancel();
            UpdateDisplay(string.Empty);
            return;
        }

        var parseStatus = InputParser.TryParse(newSearch, out double amount, out string fromRaw, out string toRaw);

        if (parseStatus == ParseResult.InvalidAmount)
        {
            _debounceTimer?.Cancel();
            _items.Clear();
            AddSingleItem("Amount too high", new NoOpCommand(), "\uE783");
            RaiseItemsChanged(_items.Count);
            return;
        }

        if (parseStatus == ParseResult.Success || parseStatus == ParseResult.AmountOnly || parseStatus == ParseResult.CurrencyOnly)
        {
            string from = string.IsNullOrEmpty(fromRaw) ? _settings.DefaultFrom : CurrencyMapper.Normalize(fromRaw);
            string to = string.IsNullOrWhiteSpace(toRaw) ? _settings.DefaultTo : CurrencyMapper.Normalize(toRaw);

            var cache = _apiService.GetCachedConversion(from, to);
            bool isCacheFresh = cache != null && (DateTime.Now - cache.OfflineDate).GetValueOrDefault().TotalMinutes < 60;

            if (isCacheFresh)
            {
                _debounceTimer?.Cancel();
                DisplayFinalLayout(amount, from, to, cache);
                return;
            }

            UpdateDisplay(newSearch, isFetching: true);

            _debounceTimer?.Cancel();
            _debounceTimer = new CancellationTokenSource();
            var token = _debounceTimer.Token;

            _ = Task.Run(async () => {
                try
                {
                    await Task.Delay(400, token);
                    if (!token.IsCancellationRequested)
                    {
                        var result = await _apiService.GetConversionAsync(from, to);
                        if (!token.IsCancellationRequested && result != null)
                        {
                            DisplayFinalLayout(amount, from, to, result);
                        }
                    }
                }
                catch (OperationCanceledException) { }
            }, token);
        }
        else
        {
            _debounceTimer?.Cancel();
            UpdateDisplay(string.Empty);
        }
    }

    private void UpdateDisplay(string search, bool isFetching = false)
    {
        _items.Clear();
        _settings = _settingsService.GetSettings(true);

        if (string.IsNullOrWhiteSpace(search))
        {
            AddSingleItem("Enter Amount...", new NoOpCommand(), "\uE8EF");

            string from = _settings.DefaultFrom;
            string to = _settings.DefaultTo;

            var cache = _apiService.GetCachedConversion(from, to);
            bool isCacheFresh = cache != null && (DateTime.Now - cache.OfflineDate).GetValueOrDefault().TotalMinutes < 60;

            if (cache != null && cache.Rates != null && cache.Rates.Any())
            {
                double rate = cache.Rates[0].Rate;
                double reverseRate = rate > 0 ? 1.0 / rate : 0;

                var displayFormat = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
                displayFormat.NumberGroupSeparator = " ";

                string formattedRate = rate.ToString("N4", displayFormat);
                string formattedReverseRate = reverseRate.ToString("N4", displayFormat);

                AddSingleItem(
                    $"1 {from} = {formattedRate} {to}",
                    new CopyTextCommand(rate.ToString("F4", CultureInfo.InvariantCulture)) { Name = "Copy Rate" },
                    "\uE825",
                    rate > 0 ? $"1 {to} = {formattedReverseRate} {from}" : "",
                    targetIso: from
                );
            }
            else
            {
                AddSingleItem($"1 {from} = ... {to}", new NoOpCommand(), "\uE94E", targetIso: from);
            }

            if (!isCacheFresh)
            {
                FetchLatestRateSilently(from, to);
            }
        }
        else
        {
            var parseStatus = InputParser.TryParse(search, out double amount, out string fromRaw, out string toRaw);

            if (parseStatus == ParseResult.Success || parseStatus == ParseResult.AmountOnly || parseStatus == ParseResult.CurrencyOnly)
            {
                string from = string.IsNullOrEmpty(fromRaw) ? _settings.DefaultFrom : CurrencyMapper.Normalize(fromRaw);
                string to = string.IsNullOrWhiteSpace(toRaw) ? _settings.DefaultTo : CurrencyMapper.Normalize(toRaw);

                string title = isFetching ? "..." : $"{amount} {from} to {to}";
                AddSingleItem(title, new NoOpCommand(), "\uE94E", targetIso: to);
            }
            else
            {
                AddSingleItem("Amount Error", new NoOpCommand(), "\uE783");
            }
        }

        RaiseItemsChanged(_items.Count);
    }

    private void DisplayFinalLayout(double amount, string from, string to, ConversionResult result)
    {
        var rateInfo = result.Rates?.FirstOrDefault();
        if (rateInfo == null) return;

        double rate = rateInfo.Rate;
        double finalValue = amount * rate;
        double reverseRate = rate > 0 ? 1.0 / rate : 0;

        var displayFormat = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        displayFormat.NumberGroupSeparator = " ";

        string formattedAmount = amount.ToString("N2", displayFormat);
        string formattedResult = finalValue.ToString("N2", displayFormat);
        string formattedRate = rate.ToString("N4", displayFormat);
        string formattedReverseRate = reverseRate.ToString("N4", displayFormat);

        _items.Clear();

        AddSingleItem(
            $"{formattedResult} {to}",
            new CopyTextCommand(finalValue.ToString("F2", CultureInfo.InvariantCulture)) { Name = "Copy Result" },
            "\uE94E",
            $"{formattedAmount} {from} = {formattedResult} {to}",
            targetIso: to
        );

        AddSingleItem(
            $"1 {from} = {formattedRate} {to}",
            new CopyTextCommand(rate.ToString("F4", CultureInfo.InvariantCulture)) { Name = "Copy Rate" },
            "\uE825",
            rate > 0 ? $"1 {to} = {formattedReverseRate} {from}" : "",
            targetIso: from
        );

        RaiseItemsChanged(_items.Count);
    }

    private void AddSingleItem(string title, ICommand cmd, string fallbackGlyph, string subtitle = "", string targetIso = "")
    {
        IconInfo finalIcon = new IconInfo(fallbackGlyph);
        string finalTitle = title;

        if (!string.IsNullOrEmpty(targetIso))
        {
            var item = CurrencyMapper.SupportedCurrencies.FirstOrDefault(c =>
                string.Equals(c.IsoCode, targetIso, StringComparison.OrdinalIgnoreCase));

            if (item != null)
            {
                if (item.IsEmoji)
                {
                    finalTitle = $"{item.Emoji} {title}";
                }
                else
                {
                    string fileName = item.FlagPath.Split('/').Last();
                    string relativePath = $"Assets\\Flags\\{fileName}";
                    string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);

                    if (System.IO.File.Exists(fullPath))
                    {
                        finalIcon = IconHelpers.FromRelativePath(relativePath);
                    }
                }
            }
        }

        var moreCommands = new IContextItem[] {
        new CommandContextItem(_currenciesPage),
        new CommandContextItem(_settingsPage),
        new CommandContextItem(_launchCommand)
    };

        _items.Add(new ListItem(cmd)
        {
            Title = finalTitle,
            Subtitle = subtitle,
            Icon = finalIcon,
            MoreCommands = moreCommands
        });
    }

    private void FetchLatestRateSilently(string from, string to)
    {
        _debounceTimer?.Cancel();
        _debounceTimer = new CancellationTokenSource();
        var token = _debounceTimer.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _apiService.GetConversionAsync(from, to);

                if (!token.IsCancellationRequested && result != null)
                {
                    UpdateDisplay(string.Empty);
                }
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    public override IListItem[] GetItems() => _items.ToArray();
}