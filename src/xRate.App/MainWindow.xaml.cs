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
    private bool _isUpdatingCombos = false;
    private CancellationTokenSource? _debounceTimer;

    public MainWindow()
    {
        this.InitializeComponent();
        InitializeWindow();

        SetHandCursor(SwapButton);
        SetHandCursor(CopyResultButton);
        SetHandCursor(CopyRateButton);
        SetHandCursor(SetDefaultButton);
        SetHandCursor(ResetDefaultButton);

        _currencyService = new CurrencyService();
        _settingsService = new SettingsService();

        _settings = _settingsService.GetSettings(true);

        LoadCurrencies();
        _isInitializing = false;

        TriggerConversion();

        this.Activated += MainWindow_Activated;
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        _settings = _settingsService.GetSettings(true);
        CheckDefaultStatus();
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
        if (_isUpdatingCombos) return;

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

        CheckDefaultStatus();
        TriggerConversion();
    }

    private void TriggerConversion()
    {
        if (_isInitializing || _isSwapping) return;

        string comboFrom = CurrencyMapper.Normalize(FromComboBox.SelectedItem?.ToString() ?? _settings.DefaultFrom);
        string comboTo = CurrencyMapper.Normalize(ToComboBox.SelectedItem?.ToString() ?? _settings.DefaultTo);
        string input = AmountTextBox.Text;

        var parseStatus = InputParser.TryParse(input, out double amount, out string fromRaw, out string toRaw);

        if (parseStatus == ParseResult.InvalidAmount)
        {
            ShowErrorState("Amount too high");
            return;
        }

        if (string.IsNullOrWhiteSpace(input) || parseStatus == ParseResult.Incomplete)
        {
            ShowEmptyState();

            var cacheEmpty = _currencyService.GetCachedConversion(comboFrom, comboTo);
            if (cacheEmpty == null) FetchLatestRate(comboFrom, comboTo);
            else UpdateRateUI(comboFrom, comboTo, cacheEmpty.Rates[0].Rate);
            return;
        }

        string finalFrom = string.IsNullOrEmpty(fromRaw) ? comboFrom : fromRaw;
        string finalTo = string.IsNullOrEmpty(toRaw) ? comboTo : toRaw;

        if (finalFrom != comboFrom || finalTo != comboTo)
        {
            _isUpdatingCombos = true;

            if (finalFrom != comboFrom) SelectCurrencyInCombo(FromComboBox, finalFrom);
            if (finalTo != comboTo) SelectCurrencyInCombo(ToComboBox, finalTo);

            _isUpdatingCombos = false;

            CheckDefaultStatus();
        }

        var cache = _currencyService.GetCachedConversion(finalFrom, finalTo);
        bool isCacheFresh = cache != null && (DateTime.Now - cache.OfflineDate).GetValueOrDefault().TotalMinutes < 60;

        if (cache != null) UpdateRateUI(finalFrom, finalTo, cache.Rates[0].Rate);
        else RateTextBlock.Text = "...";

        if (isCacheFresh)
        {
            _debounceTimer?.Cancel();
            DisplayFinalConversion(amount, finalFrom, finalTo, cache.Rates[0].Rate);
        }
        else
        {
            ResultTextBlock.Text = "...";
            FetchLatestRate(finalFrom, finalTo, amount);
        }
    }

    private void UpdateRateUI(string from, string to, double rate)
    {
        if (rate <= 0) return;

        var displayFormat = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        displayFormat.NumberGroupSeparator = " ";

        double reverseRate = rate > 0 ? 1.0 / rate : 0;

        _currentRawRate = rate.ToString("0.####", CultureInfo.InvariantCulture);

        RateTextBlock.Text = $"1 {from} = {rate.ToString("N4", displayFormat)} {to}";

        if (rate > 0)
        {
            RateSubtitleTextBlock.Text = $"1 {to} = {reverseRate.ToString("N4", displayFormat)} {from}";
            RateSubtitleTextBlock.Visibility = Visibility.Visible;
        }
        else
        {
            RateSubtitleTextBlock.Visibility = Visibility.Collapsed;
        }

        CopyRateButton.Visibility = Visibility.Visible;
    }

    private void DisplayFinalConversion(double amount, string from, string to, double rate)
    {
        double finalValue = amount * rate;
        var displayFormat = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        displayFormat.NumberGroupSeparator = " ";

        string formattedFinal = finalValue.ToString("N2", displayFormat);
        string formattedAmount = amount.ToString("N2", displayFormat);

        _currentRawResult = finalValue.ToString("F2", CultureInfo.InvariantCulture);

        ResultTextBlock.Text = $"{formattedFinal} {to}";
        ResultSubtitleTextBlock.Text = $"{formattedAmount} {from} = {formattedFinal} {to}";

        ResultSubtitleTextBlock.Visibility = Visibility.Visible;
        ResultContentPanel.Visibility = Visibility.Visible;
        EmptyStatePanel.Visibility = Visibility.Collapsed;
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

                            if (amount.HasValue) DisplayFinalConversion(amount.Value, from, to, rate);
                            else if (!string.IsNullOrWhiteSpace(AmountTextBox.Text)) TriggerConversion();
                        }
                    });
                }
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    private void CheckDefaultStatus()
    {
        if (FromComboBox?.SelectedItem == null || ToComboBox?.SelectedItem == null || DefaultActionsPanel == null) return;

        string currentFrom = CurrencyMapper.Normalize(FromComboBox.SelectedItem.ToString());
        string currentTo = CurrencyMapper.Normalize(ToComboBox.SelectedItem.ToString());

        DefaultActionsPanel.Visibility = (currentFrom != _settings.DefaultFrom || currentTo != _settings.DefaultTo)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void SetDefaultButton_Click(object sender, RoutedEventArgs e)
    {
        if (FromComboBox.SelectedItem == null || ToComboBox.SelectedItem == null) return;

        _settings.DefaultFrom = CurrencyMapper.Normalize(FromComboBox.SelectedItem.ToString());
        _settings.DefaultTo = CurrencyMapper.Normalize(ToComboBox.SelectedItem.ToString());

        await _settingsService.SaveSettingsAsync(_settings);
        DefaultActionsPanel.Visibility = Visibility.Collapsed;
    }

    private void ResetDefaultButton_Click(object sender, RoutedEventArgs e)
    {
        _isUpdatingCombos = true;

        SelectCurrencyInCombo(FromComboBox, _settings.DefaultFrom);
        SelectCurrencyInCombo(ToComboBox, _settings.DefaultTo);

        string input = AmountTextBox.Text;
        var match = System.Text.RegularExpressions.Regex.Match(input, @"[a-zA-Z\p{Sc}]");

        if (match.Success)
        {
            string cleanMath = input.Substring(0, match.Index).Trim();
            AmountTextBox.Text = cleanMath;
        }
        else
        {
            TriggerConversion();
        }

        _isUpdatingCombos = false;

        CheckDefaultStatus();
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

    private void AmountTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
    }

    private void ShowEmptyState()
    {
        EmptyStatePanel.Visibility = Visibility.Visible;
        ResultContentPanel.Visibility = Visibility.Collapsed;
        CopyResultButton.Visibility = Visibility.Collapsed;
        _currentRawResult = string.Empty;
    }

    private void ShowErrorState(string message)
    {
        EmptyStatePanel.Visibility = Visibility.Collapsed;
        ResultContentPanel.Visibility = Visibility.Visible;

        ResultTextBlock.Text = message;
        ResultSubtitleTextBlock.Visibility = Visibility.Collapsed;
        CopyResultButton.Visibility = Visibility.Collapsed;
        _currentRawResult = string.Empty;
    }
}