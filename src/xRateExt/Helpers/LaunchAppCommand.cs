using System.Diagnostics;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace xRateExt.Helpers;

internal sealed class LaunchAppCommand : InvokableCommand
{
    public override ICommandResult Invoke()
    {
        try
        {
            Process.Start(new ProcessStartInfo("xrate://") { UseShellExecute = true });

            return CommandResult.Dismiss();
        }
        catch
        {
            return CommandResult.KeepOpen();
        }
    }
}