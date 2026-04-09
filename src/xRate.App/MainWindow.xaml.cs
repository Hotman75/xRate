using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using WinRT.Interop;
using xRate.Core.Helpers;
using xRate.Core.Services;

namespace xRate.App;

public sealed partial class MainWindow : Window
{
    private readonly CurrencyService _currencyService;
    private readonly SettingsService _settingsService;
    private UserSettings _settings;
    private string _currentRawResult = string.Empty;
    private bool _isSwapping = false;
    private double _scalingFactor = 1.0;

    private const int WM_GETMINMAXINFO = 0x0024;
    private const int WM_DPICHANGED = 0x02E0;

    private const int BASE_WIDTH = 450;
    private const int BASE_HEIGHT = 550;
    private const int MIN_WIDTH = 400;
    private const int MIN_HEIGHT = 500;
    private const int MAX_WIDTH = 700;
    private const int MAX_HEIGHT = 900;

    [StructLayout(LayoutKind.Sequential)]
    struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int left; public int top; public int right; public int bottom; }

    private delegate IntPtr WinProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private WinProc _newWndProc;
    private IntPtr _oldWndProc = IntPtr.Zero;
    private IntPtr _hWnd;

    [DllImport("User32.dll")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, WinProc newLong);
    [DllImport("User32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("User32.dll", SetLastError = true)]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("User32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    public MainWindow()
    {
        this.InitializeComponent();
        InitializeWindow();

        SetHandCursor(SwapButton);
        SetHandCursor(CopyButton);
        SetHandCursor(ConvertButton);
        SetHandCursor(SetDefaultButton);

        _currencyService = new CurrencyService();
        _settingsService = new SettingsService();
        _settings = _settingsService.GetSettings();

        LoadCurrencies();
    }

    private void InitializeWindow()
    {
        this.ExtendsContentIntoTitleBar = true;

        _hWnd = WindowNative.GetWindowHandle(this);
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

    private void SetHandCursor(UIElement element)
    {
        element.PointerEntered += (s, e) =>
        {
            var property = typeof(UIElement).GetProperty("ProtectedCursor",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            property?.SetValue(element, InputSystemCursor.Create(InputSystemCursorShape.Hand));
        };

        element.PointerExited += (s, e) =>
        {
            var property = typeof(UIElement).GetProperty("ProtectedCursor",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            property?.SetValue(element, null);
        };
    }

    private void LoadCurrencies()
    {
        FromComboBox.ItemsSource = CurrencyMapper.SupportedCurrencies;
        ToComboBox.ItemsSource = CurrencyMapper.SupportedCurrencies;

        SelectCurrencyInCombo(FromComboBox, _settings.DefaultFrom);
        SelectCurrencyInCombo(ToComboBox, _settings.DefaultTo);
    }

    private void SelectCurrencyInCombo(ComboBox comboBox, string isoCode)
    {
        var item = CurrencyMapper.SupportedCurrencies.FirstOrDefault(c => c.StartsWith(isoCode));
        if (item != null)
            comboBox.SelectedItem = item;
        else
            comboBox.SelectedIndex = 0;
    }

    private async void ConvertButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AmountTextBox?.Text) ||
            FromComboBox?.SelectedItem == null ||
            ToComboBox?.SelectedItem == null)
            return;

        if (!InputParser.TryExtractAmount(AmountTextBox.Text, out double amount))
        {
            ResultTextBlock.Text = "Invalid amount";
            return;
        }

        if (Math.Abs(amount) > 1_000_000_000_000)
        {
            ResultTextBlock.Text = "Amount too high";
            return;
        }

        string from = CurrencyMapper.Normalize(FromComboBox.SelectedItem.ToString());
        string to = CurrencyMapper.Normalize(ToComboBox.SelectedItem.ToString());

        ConvertButton.IsEnabled = false;
        ResultTextBlock.Text = "Converting...";
        RateTextBlock.Visibility = Visibility.Collapsed;
        OfflineWarningTextBlock.Visibility = Visibility.Collapsed;

        var result = await _currencyService.GetConversionAsync(from, to);

        if (result != null && result.Rates != null && result.Rates.Length > 0)
        {
            double rate = result.Rates[0].Rate;
            double finalValue = amount * rate;

            var displayFormat = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
            displayFormat.NumberGroupSeparator = " ";

            _currentRawResult = finalValue.ToString("F2", CultureInfo.InvariantCulture);

            string formattedResult = finalValue.ToString("N2", displayFormat);
            string formattedAmount = amount.ToString("#,0.##", displayFormat);

            ResultTextBlock.Text = $"{formattedAmount} {from} = {formattedResult} {to}";

            string formattedRate = rate.ToString("0.####", CultureInfo.InvariantCulture);
            RateTextBlock.Text = $"1 {from} = {formattedRate} {to}";
            RateTextBlock.Visibility = Visibility.Visible;

            if (result.IsOffline && result.OfflineDate.HasValue)
            {
                OfflineWarningTextBlock.Text = $"Offline: rates from {result.OfflineDate.Value.ToString("g", CultureInfo.CurrentUICulture)}";
                OfflineWarningTextBlock.Visibility = Visibility.Visible;
            }
        }
        else
        {
            ResultTextBlock.Text = "Conversion failed (No connection/Cache)";
            RateTextBlock.Visibility = Visibility.Collapsed;
        }

        ConvertButton.IsEnabled = true;
    }

    private void OnCurrencySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        CheckDefaultStatus();

        if (!_isSwapping && AmountTextBox != null && !string.IsNullOrWhiteSpace(AmountTextBox.Text))
        {
            ConvertButton_Click(this, new RoutedEventArgs());
        }
    }

    private void SwapButton_Click(object sender, RoutedEventArgs e)
    {
        _isSwapping = true;

        var temp = FromComboBox.SelectedItem;
        FromComboBox.SelectedItem = ToComboBox.SelectedItem;
        ToComboBox.SelectedItem = temp;

        _isSwapping = false;

        CheckDefaultStatus();

        if (AmountTextBox != null && !string.IsNullOrWhiteSpace(AmountTextBox.Text))
        {
            ConvertButton_Click(this, new RoutedEventArgs());
        }
    }

    private void CheckDefaultStatus()
    {
        if (FromComboBox?.SelectedItem == null || ToComboBox?.SelectedItem == null || SetDefaultButton == null) return;

        string currentFrom = CurrencyMapper.Normalize(FromComboBox.SelectedItem.ToString());
        string currentTo = CurrencyMapper.Normalize(ToComboBox.SelectedItem.ToString());

        SetDefaultButton.Visibility = (currentFrom != _settings.DefaultFrom || currentTo != _settings.DefaultTo)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private async void SetDefaultButton_Click(object sender, RoutedEventArgs e)
    {
        if (FromComboBox.SelectedItem == null || ToComboBox.SelectedItem == null) return;

        _settings.DefaultFrom = CurrencyMapper.Normalize(FromComboBox.SelectedItem.ToString());
        _settings.DefaultTo = CurrencyMapper.Normalize(ToComboBox.SelectedItem.ToString());

        await _settingsService.SaveSettingsAsync(_settings);

        SetDefaultButton.Visibility = Visibility.Collapsed;
    }

    private void AmountTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            ConvertButton_Click(this, new RoutedEventArgs());
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentRawResult))
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(_currentRawResult);
            Clipboard.SetContent(dataPackage);
        }
    }

    private void AmountTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (AmountTextBox != null)
        {
            AmountTextBox.SelectAll();
        }
    }
}