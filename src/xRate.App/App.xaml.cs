using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;

namespace xRate.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();

        var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();

        if (activatedArgs.Kind == ExtendedActivationKind.Protocol)
        {
        }

        _window.Activate();
    }
}