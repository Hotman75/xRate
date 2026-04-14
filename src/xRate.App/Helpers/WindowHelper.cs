using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace xRate.App.Helpers;

public static class WindowHelper
{
    [DllImport("User32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct MINMAXINFO
    {
        public POINT ptReserved; public POINT ptMaxSize; public POINT ptMaxPosition;
        public POINT ptMinTrackSize; public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int x; public int y; }

    private const int GWL_WNDPROC = -4;
    private const uint WM_GETMINMAXINFO = 0x0024;

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    private static WndProcDelegate _newWndProc;
    private static IntPtr _oldWndProc = IntPtr.Zero;

    private static int _scaledMinWidth;
    private static int _scaledMinHeight;

    public static void SetupWindow(Window window, int width, int height, int minWidth, int minHeight)
    {
        IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        uint dpi = GetDpiForWindow(hwnd);
        float scale = (float)dpi / 96f;

        _scaledMinWidth = (int)(minWidth * scale);
        _scaledMinHeight = (int)(minHeight * scale);

        window.AppWindow.Resize(new Windows.Graphics.SizeInt32((int)(width * scale), (int)(height * scale)));

        _newWndProc = new WndProcDelegate(WindowProc);
        _oldWndProc = SetWindowLongPtr(hwnd, GWL_WNDPROC, Marshal.GetFunctionPointerForDelegate(_newWndProc));
    }

    private static IntPtr WindowProc(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam)
    {
        if (Msg == WM_GETMINMAXINFO)
        {
            MINMAXINFO mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

            mmi.ptMinTrackSize.x = _scaledMinWidth;
            mmi.ptMinTrackSize.y = _scaledMinHeight;

            Marshal.StructureToPtr(mmi, lParam, false);
        }

        return CallWindowProc(_oldWndProc, hWnd, Msg, wParam, lParam);
    }
}