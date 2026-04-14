using Windows.ApplicationModel.DataTransfer;

namespace xRate.App.Helpers;

public static class ClipboardHelper
{
    public static void Copy(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var dataPackage = new DataPackage();
        dataPackage.SetText(text);
        Clipboard.SetContent(dataPackage);
    }
}