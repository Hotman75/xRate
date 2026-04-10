using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using xRate.Core.Helpers;
using xRate.Core.Services;
using xRateExt.Helpers;

namespace xRateExt.Pages;

internal sealed partial class xRateExtPage : DynamicListPage
{
    private readonly List<ListItem> _items = [];
    private readonly CurrencyService _apiService = new();
    private readonly SettingsService _settingsService = new();
    private UserSettings _settings;
    private string _lastSearch = string.Empty;

    private readonly LaunchAppCommand _launchCommand = new();
    private readonly ListCurrenciesPage _currenciesPage = new();
    private readonly SettingsPage _settingsPage = new();

    public xRateExtPage()
    {
        this.Name = "xRate";
        this.Icon = IconHelpers.FromRelativePath("Assets\\icon.png");

        _settings = _settingsService.GetSettings(true);
        this.PlaceholderText = "Ex: 100 | 100 € $ | 100 EUR USD";

        RefreshList(string.Empty);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _lastSearch = newSearch;
        RefreshList(newSearch);
    }

    private void RefreshList(string search)
    {
        _items.Clear();
        _settings = _settingsService.GetSettings(true);
        var launchAction = new CommandContextItem(_launchCommand);
        var currenciesAction = new CommandContextItem(_currenciesPage);
        var settingsAction = new CommandContextItem(_settingsPage);

        if (string.IsNullOrWhiteSpace(search))
        {
            _items.Add(new ListItem(new NoOpCommand())
            {
                Title = "Ready to convert",
                Subtitle = $"Default: {_settings.DefaultFrom} to {_settings.DefaultTo}. Type an amount to start.",
                Icon = new IconInfo("\uE94E"),
                MoreCommands = [currenciesAction, settingsAction, launchAction]
            });
            RaiseItemsChanged(_items.Count);
            return;
        }

        var parseStatus = InputParser.TryParse(search, out double amount, out string fromRaw, out string toRaw);

        var displayFormat = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        displayFormat.NumberGroupSeparator = " ";

        switch (parseStatus)
        {
            case ParseResult.Incomplete:
                _items.Add(new ListItem(new NoOpCommand())
                {
                    Title = "Waiting for input...",
                    Icon = new IconInfo("\uE94E"),
                    MoreCommands = [currenciesAction, settingsAction, launchAction]
                });
                break;

            case ParseResult.InvalidAmount:
                _items.Add(new ListItem(new NoOpCommand())
                {
                    Title = "Invalid amount",
                    Subtitle = "Please enter a valid number (e.g., 1 500.50)",
                    Icon = new IconInfo("\uE94E"),
                    MoreCommands = [currenciesAction, settingsAction, launchAction]
                });
                break;

            case ParseResult.AmountOnly:
                string defFrom = _settings.DefaultFrom;
                string defTo = _settings.DefaultTo;
                var cmdAmt = new ConvertCommand(() => _ = PerformConversionAsync(amount, defFrom, defTo)) { Name = "Convert" };

                _items.Add(new ListItem(cmdAmt)
                {
                    Title = $"Convert {amount.ToString("#,0.##", displayFormat)} {defFrom} to {defTo}",
                    Subtitle = "Press Enter to fetch rates.",
                    Icon = new IconInfo("\uE94E"),
                    MoreCommands = [currenciesAction, settingsAction, launchAction]
                });
                break;

            case ParseResult.Success:
                string finalFrom = CurrencyMapper.Normalize(fromRaw);
                string finalTo = string.IsNullOrWhiteSpace(toRaw) ? _settings.DefaultTo : CurrencyMapper.Normalize(toRaw);
                if (string.IsNullOrEmpty(finalFrom)) finalFrom = _settings.DefaultFrom;

                var cmdSuccess = new ConvertCommand(() => _ = PerformConversionAsync(amount, finalFrom, finalTo)) { Name = "Convert" };

                _items.Add(new ListItem(cmdSuccess)
                {
                    Title = $"Convert {amount.ToString("#,0.##", displayFormat)} {finalFrom} to {finalTo}",
                    Subtitle = "Press Enter to fetch rates",
                    Icon = new IconInfo("\uE94E"),
                    MoreCommands = [currenciesAction, settingsAction, launchAction]
                });
                break;
        }

        RaiseItemsChanged(_items.Count);
    }

    private async Task PerformConversionAsync(double amount, string from, string to)
    {
        _items.Clear();
        var launchAction = new CommandContextItem(_launchCommand);
        var currenciesAction = new CommandContextItem(_currenciesPage);
        var settingsAction = new CommandContextItem(_settingsPage);

        _items.Add(new ListItem(new NoOpCommand())
        {
            Title = "Fetching rates...",
            Icon = new IconInfo("\uE94E"),
            MoreCommands = [currenciesAction, settingsAction, launchAction]
        });
        RaiseItemsChanged(_items.Count);

        var response = await _apiService.GetConversionAsync(from, to);

        if (_items.Count > 0 && _items[0].Title == "Fetching rates...")
        {
            _items.Clear();
            var rateInfo = response?.Rates?.FirstOrDefault();

            if (rateInfo != null)
            {
                double rate = rateInfo.Rate;
                double finalResult = amount * rate;

                var displayFormat = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
                displayFormat.NumberGroupSeparator = " ";

                string formattedResult = finalResult.ToString("N2", displayFormat);
                string formattedAmount = amount.ToString("#,0.##", displayFormat);
                string formattedRate = rate.ToString("0.####", CultureInfo.InvariantCulture);
                string rawCopyValue = finalResult.ToString("F2", CultureInfo.InvariantCulture);
                string offlineTag = (response != null && response.IsOffline) ? "[Offline] " : "";

                var copyCmd = new CopyTextCommand(rawCopyValue) { Name = "Copy Result" };

                _items.Add(new ListItem(copyCmd)
                {
                    Title = $"{formattedAmount} {from} = {formattedResult} {to}",
                    Subtitle = $"{offlineTag}Rate: 1 {from} = {formattedRate} {to}. Enter to copy.",
                    Icon = new IconInfo("\uE94E"),
                    MoreCommands = [currenciesAction, settingsAction, launchAction]
                });
            }
            else
            {
                _items.Add(new ListItem(new NoOpCommand())
                {
                    Title = "Conversion failed",
                    Subtitle = "Offline mode requires at least one previous connection.",
                    Icon = new IconInfo("\uE94E"),
                    MoreCommands = [currenciesAction, settingsAction, launchAction]
                });
            }
            RaiseItemsChanged(_items.Count);
        }
    }

    public override IListItem[] GetItems() => _items.ToArray();
}