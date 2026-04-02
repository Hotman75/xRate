using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Globalization;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
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

    public MainWindow()
    {
        this.InitializeComponent();
        this.ExtendsContentIntoTitleBar = true;
        this.SetTitleBar(AppTitleBar);

        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(450, 550));

        _currencyService = new CurrencyService();
        _settingsService = new SettingsService();
        _settings = _settingsService.GetSettings();

        LoadCurrencies();
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
        {
            comboBox.SelectedItem = item;
        }
        else
        {
            comboBox.SelectedIndex = 0;
        }
    }

    private async void ConvertButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AmountTextBox?.Text) ||
            FromComboBox?.SelectedItem == null ||
            ToComboBox?.SelectedItem == null)
            return;

        if (!double.TryParse(AmountTextBox.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double amount))
        {
            ResultTextBlock.Text = "Invalid amount";
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

        if (currentFrom != _settings.DefaultFrom || currentTo != _settings.DefaultTo)
        {
            SetDefaultButton.Visibility = Visibility.Visible;
        }
        else
        {
            SetDefaultButton.Visibility = Visibility.Collapsed;
        }
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
}