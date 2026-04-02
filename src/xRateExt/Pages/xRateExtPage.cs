using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
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
    private string _lastSearch = string.Empty;

    public xRateExtPage()
    {
        this.Name = "xRate";
        this.Icon = IconHelpers.FromRelativePath("Assets\\icon.png");
        this.PlaceholderText = "Example: 100 EUR USD or 50 € $";

        RefreshList(string.Empty);
    }

    private void AddStaticLaunchItem()
    {
        _items.Add(new ListItem(new LaunchAppCommand())
        {
            Title = "Open xRate App",
            Subtitle = "Launch the desktop application",
            Icon = this.Icon
        });
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _lastSearch = newSearch;
        RefreshList(newSearch);
    }

    private void RefreshList(string search)
    {
        _items.Clear();

        var parseStatus = InputParser.TryParse(search, out double amount, out string fromRaw, out string toRaw);

        switch (parseStatus)
        {
            case ParseResult.Incomplete:
                _items.Add(new ListItem(new NoOpCommand())
                {
                    Title = "Waiting for input...",
                    Subtitle = "Format: <amount> <from> <to> (e.g., 100 € $)",
                    Icon = new IconInfo("\uE94E")
                });
                break;

            case ParseResult.InvalidAmount:
                _items.Add(new ListItem(new NoOpCommand())
                {
                    Title = "Invalid amount",
                    Subtitle = "Please enter a valid number (e.g., 150.50)",
                    Icon = new IconInfo("\uE94E")
                });
                break;

            case ParseResult.Success:
                string fromCode = CurrencyMapper.Normalize(fromRaw);
                string toCode = CurrencyMapper.Normalize(toRaw);

                _items.Add(new ListItem(new ConvertCommand(() => _ = PerformConversionAsync(amount, fromCode, toCode)))
                {
                    Title = $"Convert {amount.ToString(CultureInfo.InvariantCulture)} {fromCode} to {toCode}",
                    Subtitle = "Press Enter to fetch live rates",
                    Icon = new IconInfo("\uE94E")
                });
                break;
        }

        AddStaticLaunchItem();
        RaiseItemsChanged(_items.Count);
    }

    private async Task PerformConversionAsync(double amount, string from, string to)
    {
        _items.Clear();
        _items.Add(new ListItem(new NoOpCommand())
        {
            Title = "Fetching rates...",
            Icon = new IconInfo("\uE94E")
        });
        RaiseItemsChanged(_items.Count);

        var response = await _apiService.GetConversionAsync(from, to);

        if (_items.Count > 0 && _items[0].Title == "Fetching rates...")
        {
            _items.Clear();
            var rateInfo = response?.FirstOrDefault();

            if (rateInfo != null)
            {
                double rate = rateInfo.Rate;
                double finalResult = amount * rate;

                string formattedResult = finalResult.ToString("N2", CultureInfo.InvariantCulture);
                string formattedAmount = amount.ToString("0.##", CultureInfo.InvariantCulture);
                string formattedRate = rate.ToString("0.####", CultureInfo.InvariantCulture);

                _items.Add(new ListItem(new CopyTextCommand(formattedResult))
                {
                    Title = $"{formattedAmount} {from} = {formattedResult} {to}",
                    Subtitle = $"Rate: 1 {from} = {formattedRate} {to}. Enter to copy.",
                    Icon = new IconInfo("\uE94E")
                });
            }
            else
            {
                _items.Add(new ListItem(new NoOpCommand())
                {
                    Title = "Conversion failed",
                    Subtitle = "Check your connection or the currency codes.",
                    Icon = new IconInfo("\uE94E")
                });
            }

            AddStaticLaunchItem();
            RaiseItemsChanged(_items.Count);
        }
    }

    public override IListItem[] GetItems() => _items.ToArray();
}