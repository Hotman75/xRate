using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using xRate.App.Helpers;
using xRate.Core.Helpers;
using xRate.Core.Models;
using xRate.Core.Services;

namespace xRate.App;

public sealed partial class MainWindow : Window
{
    private readonly CurrencyService _currencyService;
    private readonly SettingsService _settingsService;
    private UserSettings _settings;

    private string _currentRawFrom = string.Empty;
    private string _currentRawTo = string.Empty;
    private string _currentRawRate = string.Empty;

    private bool _isSwapping = false;
    private bool _isInitializing = true;
    private bool _isUpdatingCombos = false;
    private bool _isUpdating = false;
    private bool _isFromActive = true;

    private CancellationTokenSource? _debounceTimer;

    public MainWindow()
    {
        this.InitializeComponent();
        SetupWindow();

        UIHelper.SetHandCursor(SwapButton);
        UIHelper.SetHandCursor(CopyFromButton);
        UIHelper.SetHandCursor(CopyToButton);
        UIHelper.SetHandCursor(CopyRateButton);
        UIHelper.SetHandCursor(SetDefaultButton);
        UIHelper.SetHandCursor(ResetDefaultButton);

        _currencyService = new CurrencyService();
        _settingsService = new SettingsService();
        _settings = _settingsService.GetSettings(true);

        LoadCurrencies();
        _isInitializing = false;

        TriggerConversion();
        this.Activated += MainWindow_Activated;
    }

    private void SetupWindow()
    {
        WindowHelper.SetupWindow(this, 420, 540, 380, 500);

        this.ExtendsContentIntoTitleBar = true;
        this.SetTitleBar(AppTitleBar);
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        _settings = _settingsService.GetSettings(true);
        CheckDefaultStatus();
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
        if (item != null) comboBox.SelectedItem = item;
        else comboBox.SelectedIndex = 0;
    }

    private void FromAmountTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating || _isInitializing) return;

        if (FromAmountTextBox.FocusState == FocusState.Unfocused) return;

        _isFromActive = true;
        TriggerConversion();
    }

    private void ToAmountTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating || _isInitializing) return;

        if (ToAmountTextBox.FocusState == FocusState.Unfocused) return;

        _isFromActive = false;
        TriggerConversion();
    }

    private void OnCurrencySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingCombos || _isInitializing) return;

        _isFromActive = true;

        CheckDefaultStatus();
        TriggerConversion();
    }

    private void SwapButton_Click(object sender, RoutedEventArgs e)
    {
        _isSwapping = true;

        var tempCombo = FromComboBox.SelectedItem;
        FromComboBox.SelectedItem = ToComboBox.SelectedItem;
        ToComboBox.SelectedItem = tempCombo;

        _isFromActive = true;
        _isSwapping = false;

        CheckDefaultStatus();
        TriggerConversion();
    }

    private void TriggerConversion()
    {
        if (_isInitializing || _isSwapping || _isUpdating) return;

        TextBox activeBox = _isFromActive ? FromAmountTextBox : ToAmountTextBox;
        ComboBox activeCombo = _isFromActive ? FromComboBox : ToComboBox;

        string input = activeBox.Text;
        var parseStatus = InputParser.TryParse(input, out double amount, out string fromRaw, out string toRaw);

        if (parseStatus == ParseResult.InvalidAmount)
        {
            ShowErrorState("Amount too high");
            return;
        }

        if (string.IsNullOrWhiteSpace(input) || parseStatus == ParseResult.Incomplete)
        {
            ShowEmptyState();
            string cFrom = CurrencyMapper.Normalize(FromComboBox.SelectedItem?.ToString() ?? _settings.DefaultFrom);
            string cTo = CurrencyMapper.Normalize(ToComboBox.SelectedItem?.ToString() ?? _settings.DefaultTo);

            var cacheEmpty = _currencyService.GetCachedConversion(cFrom, cTo);
            if (cacheEmpty == null) FetchLatestRate(cFrom, cTo);
            else UpdateRateUI(cFrom, cTo, cacheEmpty.Rates[0].Rate);
            return;
        }

        string foundCurrency = !string.IsNullOrEmpty(fromRaw) ? fromRaw : toRaw;
        if (!string.IsNullOrEmpty(foundCurrency))
        {
            string currentComboCur = CurrencyMapper.Normalize(activeCombo.SelectedItem?.ToString() ?? string.Empty);
            if (currentComboCur != foundCurrency)
            {
                _isUpdatingCombos = true;
                SelectCurrencyInCombo(activeCombo, foundCurrency);
                _isUpdatingCombos = false;
                CheckDefaultStatus();
            }
        }

        string finalFrom = CurrencyMapper.Normalize(FromComboBox.SelectedItem?.ToString() ?? _settings.DefaultFrom);
        string finalTo = CurrencyMapper.Normalize(ToComboBox.SelectedItem?.ToString() ?? _settings.DefaultTo);

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
        else RateSubtitleTextBlock.Visibility = Visibility.Collapsed;

        CopyRateButton.Visibility = Visibility.Visible;
    }

    private void DisplayFinalConversion(double inputAmount, string from, string to, double rate)
    {
        double fromAmount, toAmount;

        if (_isFromActive)
        {
            fromAmount = inputAmount;
            toAmount = inputAmount * rate;
        }
        else
        {
            toAmount = inputAmount;
            fromAmount = rate > 0 ? inputAmount / rate : 0;
        }

        var displayFormat = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        displayFormat.NumberGroupSeparator = " ";

        string formattedFrom = fromAmount.ToString("N2", displayFormat);
        string formattedTo = toAmount.ToString("N2", displayFormat);

        _currentRawFrom = fromAmount.ToString("F2", CultureInfo.InvariantCulture);
        _currentRawTo = toAmount.ToString("F2", CultureInfo.InvariantCulture);

        _isUpdating = true;

        if (_isFromActive)
        {
            ToAmountTextBox.Text = toAmount.ToString("0.####", CultureInfo.InvariantCulture);
        }
        else
        {
            FromAmountTextBox.Text = fromAmount.ToString("0.####", CultureInfo.InvariantCulture);
        }

        _isUpdating = false;

        ResultTextBlock.Text = $"{formattedFrom} {from} = {formattedTo} {to}";

        ResultTextBlock.Visibility = Visibility.Visible;
        EmptyStatePanel.Visibility = Visibility.Collapsed;
        CopyFromButton.Opacity = 1.0;
        CopyToButton.Opacity = 1.0;
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
                        if (!token.IsCancellationRequested && result?.Rates?.Any() == true)
                        {
                            double rate = result.Rates[0].Rate;
                            UpdateRateUI(from, to, rate);
                            if (amount.HasValue) DisplayFinalConversion(amount.Value, from, to, rate);
                            else TriggerConversion();
                        }
                    });
                }
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    private void CheckDefaultStatus()
    {
        if (FromComboBox?.SelectedItem == null || ToComboBox?.SelectedItem == null) return;
        string currentFrom = CurrencyMapper.Normalize(FromComboBox.SelectedItem.ToString());
        string currentTo = CurrencyMapper.Normalize(ToComboBox.SelectedItem.ToString());
        bool isNotDefault = (currentFrom != _settings.DefaultFrom || currentTo != _settings.DefaultTo);

        ResetDefaultButton.Visibility = isNotDefault ? Visibility.Visible : Visibility.Collapsed;
        SetDefaultButton.Visibility = isNotDefault ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void SetDefaultButton_Click(object sender, RoutedEventArgs e)
    {
        if (FromComboBox.SelectedItem == null || ToComboBox.SelectedItem == null) return;
        _settings.DefaultFrom = CurrencyMapper.Normalize(FromComboBox.SelectedItem.ToString());
        _settings.DefaultTo = CurrencyMapper.Normalize(ToComboBox.SelectedItem.ToString());
        await _settingsService.SaveSettingsAsync(_settings);
        CheckDefaultStatus();
    }

    private void ResetDefaultButton_Click(object sender, RoutedEventArgs e)
    {
        _isUpdatingCombos = true;
        SelectCurrencyInCombo(FromComboBox, _settings.DefaultFrom);
        SelectCurrencyInCombo(ToComboBox, _settings.DefaultTo);
        _isUpdatingCombos = false;

        _isFromActive = true;
        CheckDefaultStatus();
        TriggerConversion();
    }

    private void CopyFromButton_Click(object sender, RoutedEventArgs e) => ClipboardHelper.Copy(_currentRawFrom);
    private void CopyToButton_Click(object sender, RoutedEventArgs e) => ClipboardHelper.Copy(_currentRawTo);
    private void CopyRateButton_Click(object sender, RoutedEventArgs e) => ClipboardHelper.Copy(_currentRawRate);

    private void AmountTextBox_GotFocus(object sender, RoutedEventArgs e) { }

    private void ShowEmptyState()
    {
        EmptyStatePanel.Visibility = Visibility.Visible;
        ResultTextBlock.Visibility = Visibility.Collapsed;
        CopyFromButton.Opacity = 0.3;
        CopyToButton.Opacity = 0.3;

        _isUpdating = true;
        if (_isFromActive) ToAmountTextBox.Text = string.Empty;
        else FromAmountTextBox.Text = string.Empty;
        _isUpdating = false;

        _currentRawFrom = string.Empty;
        _currentRawTo = string.Empty;
    }

    private void ShowErrorState(string message)
    {
        EmptyStatePanel.Visibility = Visibility.Collapsed;
        ResultTextBlock.Visibility = Visibility.Visible;
        ResultTextBlock.Text = message;
        CopyFromButton.Opacity = 0.3;
        CopyToButton.Opacity = 0.3;
    }
}