using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System.Linq;
using xRate.Core.Helpers;

namespace xRateExt.Pages;

internal sealed partial class ListCurrenciesPage : ListPage
{
    private readonly IListItem[] _items;

    public ListCurrenciesPage()
    {
        this.Name = "Supported Currencies";
        this.Icon = new IconInfo("\uE12A");

        _items = CurrencyMapper.SupportedCurrencies
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
                    Subtitle = subtitle,
                    Icon = new IconInfo("\uE1D0"),
                };
            })
            .ToArray();
    }

    public override IListItem[] GetItems() => _items;
}
