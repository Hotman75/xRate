using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System.Collections.Generic;
using System.Linq;
using xRate.Core.Helpers;

namespace xRateExt.Pages;

internal sealed partial class ListCurrenciesPage : ListPage
{
    private readonly IListItem[] _items;

    public ListCurrenciesPage()
    {
        this.Name = "Supported Currencies";

        this.Icon = new IconInfo("\uE825");

        var headerItem = new ListItem(new NoOpCommand())
        {
            Title = "Name",
            Subtitle = "ISO Code  ·  Symbol",
            Icon = new IconInfo("\uE946")
        };

        var currencyItems = CurrencyMapper.SupportedCurrencies
            .Select(entry =>
            {
                var dashIndex = entry.IndexOf(" - ");
                var iso = dashIndex >= 0 ? entry.Substring(0, dashIndex) : entry;
                var rest = dashIndex >= 0 ? entry.Substring(dashIndex + 3) : "";

                string name = rest;
                string symbol = "";
                var parenOpen = rest.LastIndexOf('(');
                var parenClose = rest.LastIndexOf(')');
                if (parenOpen >= 0 && parenClose > parenOpen)
                {
                    symbol = rest.Substring(parenOpen + 1, parenClose - parenOpen - 1);
                    name = rest.Substring(0, parenOpen).TrimEnd();
                }

                var subtitle = string.IsNullOrEmpty(symbol) ? iso : $"{iso}  ·  {symbol}";

                return (IListItem)new ListItem(new CopyTextCommand(iso) { Name = "Copy ISO Code" })
                {
                    Title = name,
                    Subtitle = subtitle
                };
            });

        var allItems = new List<IListItem> { headerItem };
        allItems.AddRange(currencyItems);

        _items = allItems.ToArray();
    }

    public override IListItem[] GetItems() => _items;
}