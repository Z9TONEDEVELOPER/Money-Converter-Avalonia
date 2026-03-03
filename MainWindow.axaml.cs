using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;           // для Loaded
using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;

namespace Money_Converter;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public class OpenExchangeRateResponse
    {
        public string Base { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public Dictionary<string, decimal> Rates { get; set; } = new();
    }

    private readonly HttpClient _httpClient = new HttpClient();

    private Dictionary<string, decimal> _exchangeRates = new();

    private ObservableCollection<string> _currencies = new();
    public ObservableCollection<string> Currencies
    {
        get => _currencies;
        set => SetField(ref _currencies, value);
    }

    private string _fromCurrency = "USD";
    public string FromCurrency
    {
        get => _fromCurrency;
        set => SetField(ref _fromCurrency, value);
    }

    private string _toCurrency = "EUR";
    public string ToCurrency
    {
        get => _toCurrency;
        set => SetField(ref _toCurrency, value);
    }

    private string _amount = "1";
    public string Amount
    {
        get => _amount;
        set => SetField(ref _amount, value);
    }

    private string _result = string.Empty;
    public string Result
    {
        get => _result;
        set => SetField(ref _result, value);
    }

    private string _statusMessage = "Обновление курсов...";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    private async Task LoadExchangeRatesAsync()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true   // игнорирует регистр
        };
        try
        {
            string url = "https://open.er-api.com/v6/latest/USD";
            string json = await _httpClient.GetStringAsync(url);

            var response = JsonSerializer.Deserialize<OpenExchangeRateResponse>(json, options);
            Console.WriteLine($"Успех! Валют загружено: {response.Rates.Count}");
            // Главная проверка: если всё хорошо
            if (response != null && response.Rates != null && response.Rates.Count > 0)
            {
                _exchangeRates = response.Rates;

                // Новая коллекция — Avalonia точно увидит
                Currencies = new ObservableCollection<string>(_exchangeRates.Keys.OrderBy(k => k));

                // Устанавливаем валюты по умолчанию
                if (Currencies.Count > 0)
                {
                    FromCurrency = Currencies.FirstOrDefault(c => c == "USD") ?? Currencies[0];
                    ToCurrency   = Currencies.FirstOrDefault(c => c == "EUR") ?? Currencies[1 % Currencies.Count];
                }

                StatusMessage = $"Курсы загружены: {response.Date} — {response.Rates.Count} валют";
            }
            else
            {
                StatusMessage = "Не удалось загрузить курсы: некорректный ответ от API";
            }
        }
        catch (JsonException ex)
        {
            StatusMessage = $"Ошибка JSON: {ex.Message}";
        }
        catch (HttpRequestException ex)
        {
            StatusMessage = $"Ошибка сети: {ex.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        // Загружаем курсы после полной загрузки окна
        Loaded += async (s, e) => await LoadExchangeRatesAsync();
    }
    private async void RevertButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_exchangeRates.Count==0)
        {
            await LoadExchangeRatesAsync();
            if (_exchangeRates.Count == 0) return;
        }
        if(!decimal.TryParse(Amount, out decimal inputAmount) || inputAmount <= 0)
        {
            Result = "Введите корректную сумму";
            return;
        }

        if (string.IsNullOrEmpty(FromCurrency) || string.IsNullOrEmpty(ToCurrency))
        {
            Result = "Выберите обе валюты";
            return;
        }
        if (!_exchangeRates.ContainsKey(FromCurrency) || !_exchangeRates.ContainsKey(ToCurrency))
        {
            Result = "Курс для выбранной валюты не найден";
            return;
        }
        decimal rateFrom = _exchangeRates[ToCurrency];
        decimal rateTo   = _exchangeRates[FromCurrency];

        decimal amountInUsd = inputAmount / rateFrom;   // From → USD
        decimal result      = amountInUsd * rateTo;     // USD → To

        Result = $"{inputAmount:F2} {ToCurrency} = {result:F2} {FromCurrency}";
    }
    private async void ConvertButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_exchangeRates.Count == 0)
        {
            await LoadExchangeRatesAsync();
            if (_exchangeRates.Count == 0) return;
        }

        if (!decimal.TryParse(Amount, out decimal inputAmount) || inputAmount <= 0)
        {
            Result = "Введите корректную сумму";
            return;
        }

        if (string.IsNullOrEmpty(FromCurrency) || string.IsNullOrEmpty(ToCurrency))
        {
            Result = "Выберите обе валюты";
            return;
        }

        if (!_exchangeRates.ContainsKey(FromCurrency) || !_exchangeRates.ContainsKey(ToCurrency))
        {
            Result = "Курс для выбранной валюты не найден";
            return;
        }

        decimal rateFrom = _exchangeRates[FromCurrency];
        decimal rateTo   = _exchangeRates[ToCurrency];

        decimal amountInUsd = inputAmount / rateFrom;   // From → USD
        decimal result      = amountInUsd * rateTo;     // USD → To

        Result = $"{inputAmount:F2} {FromCurrency} = {result:F2} {ToCurrency}";
    }

    // Стандартный INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}