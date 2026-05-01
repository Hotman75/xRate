using Microsoft.UI.Xaml;

namespace xRate.App.Helpers;

public static class UIConverters
{
    public static Visibility ShowIfEmoji(bool isEmoji) => isEmoji ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility ShowIfSvg(bool isEmoji) => isEmoji ? Visibility.Collapsed : Visibility.Visible;
}