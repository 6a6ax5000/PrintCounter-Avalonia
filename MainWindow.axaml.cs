using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PrintCounter.Avalonia;

public sealed class PrintEntry
{
    public DateTime Date { get; set; } = DateTime.Today;
    public string User { get; set; } = "";
    public int Sheets { get; set; }
    public string DateText => Date.ToString("dd.MM.yyyy");
}

public sealed class ReportRow
{
    public string User { get; set; } = "";
    public int Sheets { get; set; }
    public string AmountText { get; set; } = "0,00";
}

public sealed class AppData
{
    public List<PrintEntry> Entries { get; set; } = new();
    public decimal PeriodAmount { get; set; }
    public DateTime PeriodFrom { get; set; } = DateTime.Today.AddMonths(-1);
    public DateTime PeriodTo { get; set; } = DateTime.Today;
}

public partial class MainWindow : Window
{
    private readonly string dataFile = Path.Combine(AppContext.BaseDirectory, "print-data.json");
    private AppData data = new();
    private readonly ObservableCollection<PrintEntry> entries = new();
    private readonly ObservableCollection<ReportRow> report = new();

    public MainWindow()
    {
        InitializeComponent();
        EntriesGrid.ItemsSource = entries;
        ReportGrid.ItemsSource = report;
        LoadData();
        RefreshAll();
    }

    private void LoadData()
    {
        try
        {
            if (File.Exists(dataFile))
                data = JsonSerializer.Deserialize<AppData>(File.ReadAllText(dataFile)) ?? new AppData();
        }
        catch { data = new AppData(); }

        FromPicker.SelectedDate = data.PeriodFrom;
        ToPicker.SelectedDate = data.PeriodTo;
        EntryDatePicker.SelectedDate = DateTime.Today;
        AmountBox.Value = data.PeriodAmount;
    }

    private void SaveData()
    {
        try
        {
            File.WriteAllText(dataFile, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private DateTime PickerDate(DatePicker picker, DateTime fallback) =>
        picker.SelectedDate?.DateTime.Date ?? fallback;

    private List<PrintEntry> PeriodEntries()
    {
        var from = PickerDate(FromPicker, DateTime.Today.AddMonths(-1));
        var to = PickerDate(ToPicker, DateTime.Today);
        return data.Entries.Where(x => x.Date.Date >= from && x.Date.Date <= to).ToList();
    }

    private void RefreshAll()
    {
        data.PeriodFrom = PickerDate(FromPicker, DateTime.Today.AddMonths(-1));
        data.PeriodTo = PickerDate(ToPicker, DateTime.Today);
        data.PeriodAmount = (decimal)(AmountBox.Value ?? 0);

        entries.Clear();
        foreach (var item in data.Entries.OrderByDescending(x => x.Date)) entries.Add(item);

        RefreshReport();
        SaveData();
    }

    private void RefreshReport()
    {
        report.Clear();
        var groups = PeriodEntries()
            .GroupBy(x => x.User, StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(x => x.Key)
            .ToList();
        var totalSheets = groups.Sum(x => x.Sum(y => y.Sheets));
        var price = totalSheets == 0 ? 0m : data.PeriodAmount / totalSheets;

        foreach (var group in groups)
        {
            var sheets = group.Sum(x => x.Sheets);
            report.Add(new ReportRow
            {
                User = group.Key,
                Sheets = sheets,
                AmountText = (sheets * price).ToString("N2", CultureInfo.CurrentCulture)
            });
        }

        PriceLabel.Text = $"Цена одного листа: {price:N2} ₽";
        TotalSheetsLabel.Text = $"Всего листов: {totalSheets:N0}";
        TotalAmountLabel.Text = $"Общая сумма: {data.PeriodAmount:N2} ₽";
    }

    private void AddClick(object? sender, RoutedEventArgs e)
    {
        var user = UserBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(user))
        {
            UserBox.Focus();
            return;
        }

        data.Entries.Add(new PrintEntry
        {
            User = user,
            Sheets = Math.Max(1, (int)(SheetsBox.Value ?? 1)),
            Date = PickerDate(EntryDatePicker, DateTime.Today)
        });
        UserBox.Text = "";
        SheetsBox.Value = 1;
        SaveData();
        RefreshAll();
        UserBox.Focus();
    }

    private void RefreshClick(object? sender, RoutedEventArgs e) => RefreshAll();
}
