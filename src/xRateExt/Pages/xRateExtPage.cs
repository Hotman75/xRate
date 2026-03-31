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

    public xRateExtPage()
    {
        this.Name = "xRate Converter";
        this.Icon = IconHelpers.FromRelativePath("Assets\\icon.png");
        this.PlaceholderText = "Format: <amount> <from> <to> (e.g., 100 € $)";

        RefreshList(string.Empty);
    }

    private void AddStaticLaunchItem()
    {
        _items.Add(new ListItem(new LaunchAppCommand())
        {
            Title = "Open xRate App",
            Subtitle = "Launch the full desktop application",
            Icon = this.Icon
        });
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        RefreshList(newSearch);
    }

    private void RefreshList(string search)
    {
        _items.Clear();

        var parseStatus = InputParser.TryParse(search, out double amount, out string fromCode, out string toCode);

        switch (parseStatus)
        {
            case ParseResult.Incomplete:
                _items.Add(new ListItem(new NoOpCommand())
                {
                    Title = "Waiting for input...",
                    Subtitle = "Format: <amount> <from> <to> (e.g., 100 € $)",
                    Icon = this.Icon
                });
                break;

            case ParseResult.InvalidAmount:
                _items.Add(new ListItem(new NoOpCommand())
                {
                    Title = "Invalid amount",
                    Subtitle = "Please enter a valid number.",
                    Icon = this.Icon
                });
                break;

            case ParseResult.Success:
                _items.Add(new ListItem(new ConvertCommand(() => _ = PerformConversionAsync(amount, fromCode, toCode)))
                {
                    Title = $"Convert {amount} {fromCode} to {toCode}",
                    Subtitle = "Press Enter to fetch live rates",
                    Icon = this.Icon
                });
                break;
        }

        AddStaticLaunchItem();
        RaiseItemsChanged(_items.Count);
    }

    private async Task PerformConversionAsync(double amount, string from, string to)
    {
        _items.Clear();
        _items.Add(new ListItem(new NoOpCommand()) { Title = "Fetching rates from API...", Icon = this.Icon });
        AddStaticLaunchItem();
        RaiseItemsChanged(_items.Count);

        var response = await _apiService.GetConversionAsync(from, to);

        _items.Clear();
        var rateInfo = response?.FirstOrDefault();

        if (rateInfo != null)
        {
            double rate = rateInfo.Rate;
            double finalResult = amount * rate;
            double roundedResult = Math.Round(finalResult, 2, MidpointRounding.AwayFromZero);

            string formattedResult = $"{roundedResult.ToString("N2", CultureInfo.InvariantCulture)} {to}";
            string rawAmount = roundedResult.ToString("F2", CultureInfo.InvariantCulture);

            _items.Add(new ListItem(new CopyTextCommand(rawAmount))
            {
                Title = formattedResult,
                Subtitle = $"Rate: 1 {from} = {rate} {to}. Press Enter to copy value.",
                Icon = this.Icon
            });
        }
        else
        {
            _items.Add(new ListItem(new NoOpCommand())
            {
                Title = "Conversion failed",
                Subtitle = $"Could not find rates for {from} to {to}. Check your connection.",
                Icon = this.Icon
            });
        }

        AddStaticLaunchItem();
        RaiseItemsChanged(_items.Count);
    }

    public override IListItem[] GetItems() => _items.ToArray();
}