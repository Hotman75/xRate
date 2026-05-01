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
            Title = "ISO",
            Subtitle = "Name · Symbol",
            Icon = new IconInfo("\uE946")
        };

        var currencyItems = CurrencyMapper.SupportedCurrencies
            .Select(item =>
            {
                string subtitle = string.IsNullOrEmpty(item.Symbol)
                    ? item.Name
                    : $"{item.Name} · {item.Symbol}";

                return (IListItem)new ListItem(new CopyTextCommand(item.IsoCode) { Name = "Copy ISO Code" })
                {
                    Title = item.IsoCode,
                    Subtitle = subtitle
                };
            });

        var allItems = new List<IListItem> { headerItem };
        allItems.AddRange(currencyItems);

        _items = allItems.ToArray();
    }

    public override IListItem[] GetItems() => _items;
}