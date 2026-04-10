using System;
using System.Diagnostics;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Win32;

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
        bool isAppInstalled = false;

        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey("xrate");
            if (key != null)
            {
                using var commandKey = key.OpenSubKey(@"shell\open\command");
                if (commandKey?.GetValue(null) is string command && !string.IsNullOrEmpty(command))
                {
                    var exePath = command.Trim('"').Split('"')[0];
                    isAppInstalled = System.IO.File.Exists(exePath);
                }
            }
        }
        catch
        {
            isAppInstalled = false;
        }

        try
        {
            if (isAppInstalled)
            {
                Process.Start(new ProcessStartInfo("xrate://") { UseShellExecute = true });
            }
            else
            {
                Process.Start(new ProcessStartInfo("ms-windows-store://pdp/?ProductId=9NM38WVXBCRQ") { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            Process.Start(new ProcessStartInfo("https://apps.microsoft.com/detail/9NM38WVXBCRQ") { UseShellExecute = true });
        }

        return CommandResult.Dismiss();
    }

}