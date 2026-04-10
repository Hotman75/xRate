using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;

namespace xRate.App;

public sealed partial class MainWindow : Window
{
    private double _scalingFactor = 1.0;

    private const int WM_GETMINMAXINFO = 0x0024;
    private const int WM_DPICHANGED = 0x02E0;

    private const int BASE_WIDTH = 400;
    private const int BASE_HEIGHT = 480;

    private const int MIN_WIDTH = 380;
    private const int MIN_HEIGHT = 440;

    private const int MAX_WIDTH = 600;
    private const int MAX_HEIGHT = 800;


    struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }


    struct POINT { public int x; public int y; }


    struct RECT { public int left; public int top; public int right; public int bottom; }

    private delegate IntPtr WinProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private WinProc _newWndProc;
    private IntPtr _oldWndProc = IntPtr.Zero;
    private IntPtr _hWnd;


    [DllImport("User32.dll", EntryPoint = "GetDpiForWindow")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("User32.dll", EntryPoint = "SetWindowPos")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, WinProc newLong)
    {
        if (IntPtr.Size == 8)
            return SetWindowLongPtr64(hWnd, nIndex, newLong);
        else
            return SetWindowLong32(hWnd, nIndex, newLong);
    }

    [DllImport("User32.dll", EntryPoint = "SetWindowLongW")]
    private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, WinProc newLong);

    [DllImport("User32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, WinProc newLong);

    [DllImport("User32.dll", EntryPoint = "CallWindowProcW")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    private void InitializeWindow()
    {
        this.ExtendsContentIntoTitleBar = true;

        _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hWnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

        if (Microsoft.UI.Windowing.AppWindowTitleBar.IsCustomizationSupported())
        {
            var titleBar = appWindow.TitleBar;
            titleBar.ExtendsContentIntoTitleBar = true;
            titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
            titleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;
            titleBar.ButtonHoverBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(20, 255, 255, 255);
            titleBar.ButtonPressedBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(40, 255, 255, 255);
        }

        _scalingFactor = GetDpiForWindow(_hWnd) / 96.0;
        appWindow.Resize(new Windows.Graphics.SizeInt32(
            (int)(BASE_WIDTH * _scalingFactor),
            (int)(BASE_HEIGHT * _scalingFactor)
        ));

        var presenter = appWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
        if (presenter != null)
        {
            presenter.IsMinimizable = true;
            presenter.IsMaximizable = false;
            presenter.IsResizable = true;
        }

        this.SetTitleBar(AppTitleBar);

        _newWndProc = new WinProc(WindowProcess);
        _oldWndProc = SetWindowLongPtr(_hWnd, -4, _newWndProc);
    }

    private IntPtr WindowProcess(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_GETMINMAXINFO:
                OnGetMinMaxInfo(hWnd, lParam);
                break;

            case WM_DPICHANGED:
                OnDpiChanged(hWnd, wParam, lParam);
                return IntPtr.Zero;
        }

        return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    private void OnGetMinMaxInfo(IntPtr hWnd, IntPtr lParam)
    {
        double factor = GetDpiForWindow(hWnd) / 96.0;
        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

        mmi.ptMinTrackSize.x = (int)(MIN_WIDTH * factor);
        mmi.ptMinTrackSize.y = (int)(MIN_HEIGHT * factor);
        mmi.ptMaxTrackSize.x = (int)(MAX_WIDTH * factor);
        mmi.ptMaxTrackSize.y = (int)(MAX_HEIGHT * factor);

        Marshal.StructureToPtr(mmi, lParam, false);
    }

    private void OnDpiChanged(IntPtr hWnd, IntPtr wParam, IntPtr lParam)
    {
        int newDpi = (int)(wParam.ToInt64() & 0xFFFF);
        _scalingFactor = newDpi / 96.0;

        var suggestedRect = Marshal.PtrToStructure<RECT>(lParam);

        int newWidth = suggestedRect.right - suggestedRect.left;
        int newHeight = suggestedRect.bottom - suggestedRect.top;

        newWidth = Math.Clamp(newWidth, (int)(MIN_WIDTH * _scalingFactor), (int)(MAX_WIDTH * _scalingFactor));
        newHeight = Math.Clamp(newHeight, (int)(MIN_HEIGHT * _scalingFactor), (int)(MAX_HEIGHT * _scalingFactor));

        SetWindowPos(
            hWnd,
            IntPtr.Zero,
            suggestedRect.left, suggestedRect.top,
            newWidth, newHeight,
            SWP_NOZORDER | SWP_NOACTIVATE
        );
    }
}