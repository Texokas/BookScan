using System.Windows;
using System.Windows.Media.Imaging;
using LibraryBarcodeApp.Helpers;
using LibraryBarcodeApp.Models;
using LibraryBarcodeApp.Services;
using LibraryBarcodeApp.ViewModels;
using Microsoft.Win32;

namespace LibraryBarcodeApp;

public partial class AddBookWindow : Window
{
    private readonly SupabaseService _supabaseService;
    private readonly AddBookViewModel _viewModel;

    public AddBookWindow(SupabaseService supabaseService)
    {
        InitializeComponent();
        _supabaseService = supabaseService;
        _viewModel = new AddBookViewModel();
        DataContext = _viewModel;
    }

    private void LoadBarcodeImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var imagePath = dialog.FileName;
        _viewModel.BarcodeImagePreview = new BitmapImage(new Uri(imagePath));
        _viewModel.StatusMessage = "Распознавание штрихкода...";

        var decodedBarcode = BarcodeHelper.DecodeBarcodeFromImage(imagePath);
        if (string.IsNullOrWhiteSpace(decodedBarcode))
        {
            _viewModel.StatusMessage = "Штрихкод не распознан. Введите вручную.";
            return;
        }

        _viewModel.Barcode = decodedBarcode;
        _viewModel.StatusMessage = $"Штрихкод распознан: {decodedBarcode}";
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var validationError = ValidateInput();
            if (!string.IsNullOrEmpty(validationError))
            {
                _viewModel.StatusMessage = validationError;
                return;
            }

            var barcode = _viewModel.Barcode.Trim();
            var existingBook = await _supabaseService.GetBookByBarcode(barcode);
            if (existingBook is not null)
            {
                _viewModel.StatusMessage = "Книга с таким штрихкодом уже существует.";
                return;
            }

            var book = new Book
            {
                Id = Guid.NewGuid(),
                Barcode = barcode,
                Title = _viewModel.Title.Trim(),
                Author = _viewModel.Author.Trim(),
                Year = int.Parse(_viewModel.Year.Trim()),
                Category = _viewModel.SelectedCategory,
                Status = "available",
                CreatedAt = DateTime.UtcNow
            };

            await _supabaseService.AddBook(book);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"Ошибка сохранения: {ex.Message}";
        }
    }

    private string? ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(_viewModel.Barcode))
            return "Введите штрихкод.";
        if (string.IsNullOrWhiteSpace(_viewModel.Title))
            return "Введите название.";
        if (string.IsNullOrWhiteSpace(_viewModel.Author))
            return "Введите автора.";
        if (string.IsNullOrWhiteSpace(_viewModel.SelectedCategory))
            return "Выберите категорию.";

        if (!int.TryParse(_viewModel.Year, out var year))
            return "Год должен быть числом.";

        var nowYear = DateTime.Now.Year + 1;
        if (year < 1000 || year > nowYear)
            return "Год вне допустимого диапазона.";

        return null;
    }
}
