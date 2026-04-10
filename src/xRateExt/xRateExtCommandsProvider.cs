using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using xRateExt.Pages;

namespace xRateExt;

public partial class xRateExtCommandsProvider : CommandProvider
{
    public const string ProviderId = "com.hotman.xrate";
    private readonly ICommandItem[] _commands;

    public xRateExtCommandsProvider()
    {
        Id = ProviderId;
        DisplayName = "xRate";

        var icon = IconHelpers.FromRelativePath("Assets\\Ext_icon.png");

        _commands = [
            new ListItem(new xRateExtPage())
            {
                Title = "xRate",
                Icon = icon
            }
        ];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;
}