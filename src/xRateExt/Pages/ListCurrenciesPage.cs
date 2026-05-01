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
            Title = "Code",
            Subtitle = "Name · Symbol",
            Icon = new IconInfo("\uE946")
        };

        var currencyItems = CurrencyMapper.SupportedCurrencies.Select(item =>
        {
            string subtitle = string.IsNullOrEmpty(item.Symbol)
                ? item.Name
                : $"{item.Name} · {item.Symbol}";

            string title = item.IsEmoji ? $"{item.Emoji} {item.IsoCode}" : item.IsoCode;

            return (IListItem)new ListItem(new CopyTextCommand(item.IsoCode) { Name = "Copy ISO Code" })
            {
                Title = title,
                Subtitle = subtitle,
                Icon = new IconInfo("\uE825")
            };
        });

        _items = new List<IListItem> { headerItem }.Concat(currencyItems).ToArray();
    }

    public override IListItem[] GetItems() => _items;
}