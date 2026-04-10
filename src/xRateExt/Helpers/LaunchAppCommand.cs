using System;
using System.Diagnostics;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace xRateExt.Helpers;

internal sealed class LaunchAppCommand : InvokableCommand
{
    public LaunchAppCommand()
    {
        this.Name = "Desktop App";
        this.Icon = new IconInfo("\uE8A7");
    }

    public override ICommandResult Invoke()
    {
        var xrateUri = new Uri("xrate://");

        var supportStatus = Launcher
            .QueryUriSupportAsync(xrateUri, LaunchQuerySupportType.Uri)
            .GetAwaiter()
            .GetResult();

        try
        {
            if (supportStatus == LaunchQuerySupportStatus.Available)
                Launcher.LaunchUriAsync(xrateUri);
            else
                Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?ProductId=9NM38WVXBCRQ"));
        }
        catch
        {
            Process.Start(new ProcessStartInfo("https://apps.microsoft.com/detail/9NM38WVXBCRQ") { UseShellExecute = true });
        }

        return CommandResult.Dismiss();
    }
}