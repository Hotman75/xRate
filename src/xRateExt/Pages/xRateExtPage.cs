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

        var parseStatus = InputParser.TryParse(newSearch, out double amount, out string fromRaw, out string toRaw);

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

        string formattedResult = finalValue.ToString("N2", displayFormat);
        string formattedRate = rate.ToString("0.####", CultureInfo.InvariantCulture);

        _items.Clear();

        AddSingleItem(
            $"{formattedResult} {to}",
            new CopyTextCommand(finalValue.ToString("F2", CultureInfo.InvariantCulture)) { Name = "Copy Result" },
            "\uE94E"
        );

        AddSingleItem(
            $"1 {from} = {formattedRate} {to}",
            new CopyTextCommand(formattedRate) { Name = "Copy Rate" },
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
            AddSingleItem(string.Empty, new NoOpCommand(), "\uE94E");
        }
        else
        {
            var parseStatus = InputParser.TryParse(search, out double amount, out string fromRaw, out string toRaw);
            if (parseStatus == ParseResult.Success || parseStatus == ParseResult.AmountOnly)
            {
                string from = string.IsNullOrEmpty(fromRaw) ? _settings.DefaultFrom : CurrencyMapper.Normalize(fromRaw);
                string to = string.IsNullOrWhiteSpace(toRaw) ? _settings.DefaultTo : CurrencyMapper.Normalize(toRaw);

                string title = isFetching ? "Fetching rates..." : $"{amount} {from} to {to}";
                AddSingleItem(title, new NoOpCommand(), "\uE94E");
            }
            else
            {
                AddSingleItem(string.Empty, new NoOpCommand(), "\uE94E");
            }
        }

        RaiseItemsChanged(_items.Count);
    }

    private void AddSingleItem(string title, ICommand cmd, string iconGlyph)
    {
        var moreCommands = new IContextItem[] {
            new CommandContextItem(_currenciesPage),
            new CommandContextItem(_settingsPage),
            new CommandContextItem(_launchCommand)
        };

        _items.Add(new ListItem(cmd)
        {
            Title = title,
            Subtitle = string.Empty,
            Icon = new IconInfo(iconGlyph),
            MoreCommands = moreCommands
        });
    }

    public override IListItem[] GetItems() => _items.ToArray();
}