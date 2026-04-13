using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;
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
    private readonly CurrencyService _currencyService = new();

    public xRateExtPage()
    {
        this.Name = "xRate";
        this.Icon = IconHelpers.FromRelativePath("Assets\\Ext_icon.png");
        _settings = _settingsService.GetSettings(true);
        this.PlaceholderText = "Amount <From> <To> (e.g. 100 USD EUR)";

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

        double amount = 0;
        string fromRaw = string.Empty;
        string toRaw = string.Empty;

        var parseStatus = InputParser.TryParse(newSearch, out amount, out fromRaw, out toRaw);

        if (parseStatus == ParseResult.InvalidAmount)
        {
            _debounceTimer?.Cancel();
            _items.Clear();
            AddSingleItem("Amount too high", new NoOpCommand(), "\uE783");
            RaiseItemsChanged(_items.Count);
            return;
        }

        if (parseStatus == ParseResult.Success || parseStatus == ParseResult.AmountOnly)
        {
            string from = string.IsNullOrEmpty(fromRaw) ? _settings.DefaultFrom : CurrencyMapper.Normalize(fromRaw);
            string to = string.IsNullOrWhiteSpace(toRaw) ? _settings.DefaultTo : CurrencyMapper.Normalize(toRaw);

            var cache = _apiService.GetCachedConversion(from, to);

            if (cache != null && (DateTime.Now - cache.OfflineDate).GetValueOrDefault().TotalMinutes < 60)
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

    private void DisplayFinalLayout(double amount, string from, string to, ConversionResult result)
    {
        var rateInfo = result.Rates?.FirstOrDefault();
        if (rateInfo == null) return;

        double rate = rateInfo.Rate;
        double finalValue = amount * rate;

        var displayFormat = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        displayFormat.NumberGroupSeparator = " ";

        string formattedAmount = amount.ToString("N2", displayFormat);
        string formattedResult = finalValue.ToString("N2", displayFormat);

        string formattedRate = rate.ToString("N4", displayFormat);

        _items.Clear();

        AddSingleItem(
            $"{formattedResult} {to}",
            new CopyTextCommand(finalValue.ToString("F2", CultureInfo.InvariantCulture)) { Name = "Copy Result" },
            "\uE94E",
            $"{formattedAmount} {from} = {formattedResult} {to}"
        );

        AddSingleItem(
            $"1 {from} = {formattedRate} {to}",
            new CopyTextCommand(rate.ToString("F4", CultureInfo.InvariantCulture)) { Name = "Copy Rate" },
            "\uE8EF"
        );

        RaiseItemsChanged(_items.Count);
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

            var cache = _currencyService.GetCachedConversion(from, to);
            bool isCacheFresh = cache != null && (DateTime.Now - cache.OfflineDate).GetValueOrDefault().TotalMinutes < 60;

            if (cache != null && cache.Rates != null && cache.Rates.Any())
            {
                double rate = cache.Rates[0].Rate;
                var displayFormat = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
                displayFormat.NumberGroupSeparator = " ";

                string formattedRate = rate.ToString("N4", displayFormat);

                AddSingleItem(
                    $"1 {from} = {formattedRate} {to}",
                    new CopyTextCommand(rate.ToString("F4", CultureInfo.InvariantCulture)) { Name = "Copy Rate" },
                    "\uE94E"
                );
            }
            else
            {
                AddSingleItem($"1 {from} = ... {to}", new NoOpCommand(), "\uE94E");
            }

            if (!isCacheFresh)
            {
                FetchLatestRate(from, to);
            }
        }
        else
        {
            double amount = 0;
            string fromRaw = string.Empty;
            string toRaw = string.Empty;

            var parseStatus = InputParser.TryParse(search, out amount, out fromRaw, out toRaw);

            if (parseStatus == ParseResult.Success || parseStatus == ParseResult.AmountOnly)
            {
                string from = string.IsNullOrEmpty(fromRaw) ? _settings.DefaultFrom : CurrencyMapper.Normalize(fromRaw);
                string to = string.IsNullOrWhiteSpace(toRaw) ? _settings.DefaultTo : CurrencyMapper.Normalize(toRaw);

                string title = isFetching ? "..." : $"{amount} {from} to {to}";
                AddSingleItem(title, new NoOpCommand(), "\uE94E");
            }
            else
            {
                AddSingleItem("Amount Error", new NoOpCommand(), "\uE783");
            }
        }

        RaiseItemsChanged(_items.Count);
    }

    private void AddSingleItem(string title, ICommand cmd, string iconGlyph, string subtitle = "")
    {
        var moreCommands = new IContextItem[] {
            new CommandContextItem(_currenciesPage),
            new CommandContextItem(_settingsPage),
            new CommandContextItem(_launchCommand)
        };

        _items.Add(new ListItem(cmd)
        {
            Title = title,
            Subtitle = subtitle,
            Icon = new IconInfo(iconGlyph),
            MoreCommands = moreCommands
        });
    }

    private void FetchLatestRate(string from, string to)
    {
        _debounceTimer?.Cancel();
        _debounceTimer = new CancellationTokenSource();
        var token = _debounceTimer.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(400, token);

                if (!token.IsCancellationRequested)
                {
                    var result = await _currencyService.GetConversionAsync(from, to);

                    if (!token.IsCancellationRequested && result != null)
                    {
                        UpdateDisplay(string.Empty);
                    }
                }
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    public override IListItem[] GetItems() => _items.ToArray();
}