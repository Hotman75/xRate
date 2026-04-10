using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using xRate.Core.Helpers;
using xRate.Core.Models;
using xRate.Core.Services;

namespace xRate.App;

public sealed partial class MainWindow : Window
{
    private readonly CurrencyService _currencyService;
    private readonly SettingsService _settingsService;
    private UserSettings _settings;

    private string _currentRawResult = string.Empty;
    private string _currentRawRate = string.Empty;

    private bool _isSwapping = false;
    private bool _isInitializing = true;
    private CancellationTokenSource? _debounceTimer;

    public MainWindow()
    {
        this.InitializeComponent();
        InitializeWindow();

        SetHandCursor(SwapButton);
        SetHandCursor(CopyResultButton);
        SetHandCursor(CopyRateButton);
        SetHandCursor(SetDefaultButton);

        _currencyService = new CurrencyService();
        _settingsService = new SettingsService();
        _settings = _settingsService.GetSettings();

        LoadCurrencies();
        _isInitializing = false;

        UpdateInputCurrencySymbol();
        TriggerConversion();
    }

    private void SetHandCursor(UIElement element)
    {
        element.PointerEntered += (s, e) => {
            var property = typeof(UIElement).GetProperty("ProtectedCursor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            property?.SetValue(element, InputSystemCursor.Create(InputSystemCursorShape.Hand));
        };
        element.PointerExited += (s, e) => {
            var property = typeof(UIElement).GetProperty("ProtectedCursor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
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

    private void AmountTextBox_TextChanged(object sender, TextChangedEventArgs e) => TriggerConversion();

    private void OnCurrencySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateInputCurrencySymbol();
        CheckDefaultStatus();
        TriggerConversion();
    }

    private void SwapButton_Click(object sender, RoutedEventArgs e)
    {
        _isSwapping = true;
        var temp = FromComboBox.SelectedItem;
        FromComboBox.SelectedItem = ToComboBox.SelectedItem;
        ToComboBox.SelectedItem = temp;
        _isSwapping = false;

        UpdateInputCurrencySymbol();
        CheckDefaultStatus();
        TriggerConversion();
    }

    private void UpdateInputCurrencySymbol()
    {
        if (FromComboBox.SelectedItem != null && InputCurrencySymbol != null)
        {
            InputCurrencySymbol.Text = CurrencyMapper.Normalize(FromComboBox.SelectedItem.ToString());
        }
    }

    private void TriggerConversion()
    {
        if (_isInitializing || _isSwapping) return;

        string from = CurrencyMapper.Normalize(FromComboBox.SelectedItem?.ToString() ?? _settings.DefaultFrom);
        string to = CurrencyMapper.Normalize(ToComboBox.SelectedItem?.ToString() ?? _settings.DefaultTo);
        string input = AmountTextBox.Text;

        UpdateInputCurrencySymbol();

        var cache = _currencyService.GetCachedConversion(from, to);
        if (cache != null)
        {
            UpdateRateUI(from, to, cache.Rates.FirstOrDefault()?.Rate ?? 0);
        }
        else
        {
            RateTextBlock.Text = "...";
            FetchLatestRate(from, to);
        }

        if (string.IsNullOrWhiteSpace(input) || !InputParser.TryExtractAmount(input, out double amount))
        {
            ResultTextBlock.Text = string.Empty;
            CopyResultButton.Visibility = Visibility.Collapsed;
            _currentRawResult = string.Empty;
            return;
        }

        if (cache != null && (DateTime.Now - cache.OfflineDate).GetValueOrDefault().TotalMinutes < 60)
        {
            _debounceTimer?.Cancel();
            DisplayFinalConversion(amount, to, cache.Rates.FirstOrDefault()?.Rate ?? 0);
        }
        else
        {
            ResultTextBlock.Text = "...";
            FetchLatestRate(from, to, amount);
        }
    }

    private void UpdateRateUI(string from, string to, double rate)
    {
        if (rate <= 0) return;
        _currentRawRate = rate.ToString("0.####", CultureInfo.InvariantCulture);
        RateTextBlock.Text = $"1 {from} = {_currentRawRate} {to}";
        CopyRateButton.Visibility = Visibility.Visible;
    }

    private void DisplayFinalConversion(double amount, string to, double rate)
    {
        double finalValue = amount * rate;
        var displayFormat = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        displayFormat.NumberGroupSeparator = " ";

        _currentRawResult = finalValue.ToString("F2", CultureInfo.InvariantCulture);
        ResultTextBlock.Text = $"{finalValue.ToString("N2", displayFormat)} {to}";
        CopyResultButton.Visibility = Visibility.Visible;
    }

    private void FetchLatestRate(string from, string to, double? amount = null)
    {
        _debounceTimer?.Cancel();
        _debounceTimer = new CancellationTokenSource();
        var token = _debounceTimer.Token;

        _ = Task.Run(async () => {
            try
            {
                await Task.Delay(400, token);
                if (!token.IsCancellationRequested)
                {
                    var result = await _currencyService.GetConversionAsync(from, to);

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (!token.IsCancellationRequested && result != null && result.Rates != null && result.Rates.Any())
                        {
                            double rate = result.Rates[0].Rate;
                            UpdateRateUI(from, to, rate);

                            if (amount.HasValue)
                            {
                                DisplayFinalConversion(amount.Value, to, rate);
                            }
                        }
                    });
                }
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    private void CheckDefaultStatus()
    {
        if (FromComboBox?.SelectedItem == null || ToComboBox?.SelectedItem == null || SetDefaultButton == null) return;

        string currentFrom = CurrencyMapper.Normalize(FromComboBox.SelectedItem.ToString());
        string currentTo = CurrencyMapper.Normalize(ToComboBox.SelectedItem.ToString());

        SetDefaultButton.Visibility = (currentFrom != _settings.DefaultFrom || currentTo != _settings.DefaultTo)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void SetDefaultButton_Click(object sender, RoutedEventArgs e)
    {
        if (FromComboBox.SelectedItem == null || ToComboBox.SelectedItem == null) return;

        _settings.DefaultFrom = CurrencyMapper.Normalize(FromComboBox.SelectedItem.ToString());
        _settings.DefaultTo = CurrencyMapper.Normalize(ToComboBox.SelectedItem.ToString());

        await _settingsService.SaveSettingsAsync(_settings);
        SetDefaultButton.Visibility = Visibility.Collapsed;
    }

    private void CopyResultButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentRawResult))
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(_currentRawResult);
            Clipboard.SetContent(dataPackage);
        }
    }

    private void CopyRateButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentRawRate))
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(_currentRawRate);
            Clipboard.SetContent(dataPackage);
        }
    }

    private void AmountTextBox_GotFocus(object sender, RoutedEventArgs e) => AmountTextBox?.SelectAll();
}