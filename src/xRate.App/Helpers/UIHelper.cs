using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using System.Reflection;

namespace xRate.App.Helpers;

public static class UIHelper
{
    public static void SetHandCursor(UIElement element)
    {
        element.PointerEntered += (s, e) => {
            var property = typeof(UIElement).GetProperty("ProtectedCursor", BindingFlags.Instance | BindingFlags.NonPublic);
            property?.SetValue(element, InputSystemCursor.Create(InputSystemCursorShape.Hand));
        };
        element.PointerExited += (s, e) => {
            var property = typeof(UIElement).GetProperty("ProtectedCursor", BindingFlags.Instance | BindingFlags.NonPublic);
            property?.SetValue(element, null);
        };
    }
}