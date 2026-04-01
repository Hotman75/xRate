using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Linq;
using System.Globalization;
using Windows.ApplicationModel.DataTransfer;
using xRate.Core.Helpers;
using xRate.Core.Services;

namespace xRate.App;

public sealed partial class MainWindow : Window
{
    private readonly CurrencyService _currencyService = new();

    public MainWindow()
    {
        this.InitializeComponent();

        IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

        appWindow.SetIcon("Assets\\icon.png");

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        if (appWindow != null)
        {
            appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 450, Height = 580 });
        }

        FromComboBox.SelectionChanged -= OnCurrencySelectionChanged;
        ToComboBox.SelectionChanged -= OnCurrencySelectionChanged;

        FromComboBox.ItemsSource = CurrencyMapper.SupportedCurrencies;
        ToComboBox.ItemsSource = CurrencyMapper.SupportedCurrencies;

        FromComboBox.SelectedItem = CurrencyMapper.SupportedCurrencies.FirstOrDefault(c => c.StartsWith("EUR"));
        ToComboBox.SelectedItem = CurrencyMapper.SupportedCurrencies.FirstOrDefault(c => c.StartsWith("USD"));

        FromComboBox.SelectionChanged += OnCurrencySelectionChanged;
        ToComboBox.SelectionChanged += OnCurrencySelectionChanged;
    }

    private async void ConvertButton_Click(object sender, RoutedEventArgs e)
    {
        string rawInput = AmountTextBox.Text.Replace(',', '.').Trim();

        RateTextBlock.Visibility = Visibility.Collapsed;

        if (string.IsNullOrWhiteSpace(rawInput) ||
            !double.TryParse(rawInput, NumberStyles.Any, CultureInfo.InvariantCulture, out double amount))
        {
            ResultTextBlock.Text = "Please enter a valid number.";
            ShowResultWithAnimation();
            return;
        }

        string from = CurrencyMapper.Normalize(FromComboBox.SelectedItem as string ?? "EUR");
        string to = CurrencyMapper.Normalize(ToComboBox.SelectedItem as string ?? "USD");

        ResultTextBlock.Text = "Fetching rates...";
        ShowResultWithAnimation();

        try
        {
            var rates = await _currencyService.GetConversionAsync(from, to);

            if (rates != null && rates.Length > 0)
            {
                double rate = rates[0].Rate;
                double result = amount * rate;

                string formattedAmount = amount.ToString("0.##", CultureInfo.InvariantCulture);
                string formattedResult = result.ToString("N2", CultureInfo.InvariantCulture);
                string formattedRate = rate.ToString("0.####", CultureInfo.InvariantCulture);

                ResultTextBlock.Text = $"{formattedAmount} {from} = {formattedResult} {to}";

                RateTextBlock.Text = $"1 {from} = {formattedRate} {to}";
                RateTextBlock.Visibility = Visibility.Visible;
            }
            else
            {
                ResultTextBlock.Text = "Service unavailable.";
            }
        }
        catch
        {
            ResultTextBlock.Text = "An error occurred.";
        }
    }

    private void SwapButton_Click(object sender, RoutedEventArgs e)
    {
        FromComboBox.SelectionChanged -= OnCurrencySelectionChanged;
        ToComboBox.SelectionChanged -= OnCurrencySelectionChanged;

        var temp = FromComboBox.SelectedItem;
        FromComboBox.SelectedItem = ToComboBox.SelectedItem;
        ToComboBox.SelectedItem = temp;

        FromComboBox.SelectionChanged += OnCurrencySelectionChanged;
        ToComboBox.SelectionChanged += OnCurrencySelectionChanged;

        if (!string.IsNullOrWhiteSpace(AmountTextBox.Text))
        {
            ConvertButton_Click(this, new RoutedEventArgs());
        }
    }

    private void OnCurrencySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AmountTextBox != null && !string.IsNullOrWhiteSpace(AmountTextBox.Text))
        {
            ConvertButton_Click(this, new RoutedEventArgs());
        }
    }

    private void ShowResultWithAnimation()
    {
        ResultBorder.Visibility = Visibility.Visible;

        if (ResultBorder.Opacity < 1.0)
        {
            Storyboard storyboard = new Storyboard();
            DoubleAnimation fade = new DoubleAnimation
            {
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(fade, ResultBorder);
            Storyboard.SetTargetProperty(fade, "Opacity");
            storyboard.Children.Add(fade);
            storyboard.Begin();
        }
    }

    private void AmountTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            ConvertButton_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ResultTextBlock.Text) || ResultTextBlock.Text.Contains("...")) return;

        var parts = ResultTextBlock.Text.Split('=');
        if (parts.Length > 1)
        {
            var resultPart = parts[1].Trim().Split(' ')[0];
            var dataPackage = new DataPackage();
            dataPackage.SetText(resultPart);
            Clipboard.SetContent(dataPackage);
        }
    }
}