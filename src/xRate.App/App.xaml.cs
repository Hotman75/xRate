using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using WinRT.Interop;
using System;
using System.Runtime.InteropServices;
using xRate.Core.Helpers;

namespace xRate.App;

public partial class App : Application
{
    private Window? _window;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        PathHelper.Initialize();
        _window = new MainWindow();

        var currentInstance = AppInstance.GetCurrent();

        HandleActivation(currentInstance.GetActivatedEventArgs().Kind);

        currentInstance.Activated += (s, e) =>
        {
            var kind = e.Kind;

            _window.DispatcherQueue.TryEnqueue(() =>
            {
                HandleActivation(kind);
            });
        };

        _window.Activate();
    }

    private void HandleActivation(ExtendedActivationKind kind)
    {
        if (_window == null) return;

        IntPtr hwnd = WindowNative.GetWindowHandle(_window);
        ShowWindow(hwnd, SW_RESTORE);
        SetForegroundWindow(hwnd);

        _window.Activate();

        if (kind == ExtendedActivationKind.Protocol)
        {
        }
    }
}