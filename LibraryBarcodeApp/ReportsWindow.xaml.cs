using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using LibraryBarcodeApp.Services;
using Microsoft.Win32;

namespace LibraryBarcodeApp;

public partial class ReportsWindow : Window
{
    private readonly SupabaseService _supabaseService;
    private List<OperationReportRow> _currentRows = new();

    public ReportsWindow(SupabaseService supabaseService)
    {
        InitializeComponent();
        _supabaseService = supabaseService;
        StartDatePicker.SelectedDate = DateTime.Today.AddMonths(-1);
        EndDatePicker.SelectedDate = DateTime.Today;
        SummaryTextBlock.Text = "Выберите период и сформируйте отчет.";
    }

    private async void GenerateReport_Click(object sender, RoutedEventArgs e)
    {
        var start = StartDatePicker.SelectedDate;
        var end = EndDatePicker.SelectedDate;

        if (start is null || end is null)
        {
            MessageBox.Show("Выберите даты начала и конца периода.", "Отчеты", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (start > end)
        {
            MessageBox.Show("Дата начала не может быть больше даты конца.", "Отчеты", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var startDate = start.Value.Date;
            var endDate = end.Value.Date.AddDays(1).AddTicks(-1);

            var operations = await _supabaseService.GetOperationsByDateRange(startDate, endDate);
            var books = await _supabaseService.GetBooks();
            var booksMap = books.ToDictionary(b => b.Id, b => b);

            _currentRows = operations
                .OrderByDescending(o => o.Timestamp)
                .Select(o =>
                {
                    booksMap.TryGetValue(o.BookId, out var book);
                    return new OperationReportRow
                    {
                        Timestamp = o.Timestamp.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture),
                        OperationType = o.OperationType,
                        Barcode = book?.Barcode ?? "-",
                        Title = book?.Title ?? "(книга удалена)",
                        Author = book?.Author ?? "-"
                    };
                })
                .ToList();

            OperationsDataGrid.ItemsSource = _currentRows;

            var borrowedCount = operations.Count(o => o.OperationType == "borrow");
            var returnedCount = operations.Count(o => o.OperationType == "return");

            SummaryTextBlock.Text = $"Операций: {_currentRows.Count} | Выдач: {borrowedCount} | Возвратов: {returnedCount}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось сформировать отчет.\n{ex.Message}", "Отчеты", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRows.Count == 0)
        {
            MessageBox.Show("Нет данных для экспорта. Сначала сформируйте отчет.", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|Excel compatible (*.csv)|*.csv",
            FileName = $"report_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("Дата/время;Операция;Штрихкод;Название;Автор");
            foreach (var row in _currentRows)
            {
                sb.AppendLine($"{Escape(row.Timestamp)};{Escape(row.OperationType)};{Escape(row.Barcode)};{Escape(row.Title)};{Escape(row.Author)}");
            }

            File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show("Отчет успешно экспортирован в CSV.", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка экспорта.\n{ex.Message}", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string Escape(string value)
    {
        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private sealed class OperationReportRow
    {
        public string Timestamp { get; set; } = string.Empty;
        public string OperationType { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
    }
}
