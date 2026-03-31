using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;

namespace xRateExt.Helpers;

internal sealed class ConvertCommand : InvokableCommand
{
    private readonly Action _action;

    public ConvertCommand(Action action)
    {
        _action = action;
    }

    public override ICommandResult Invoke()
    {
        _action();

        return CommandResult.KeepOpen();
    }
}