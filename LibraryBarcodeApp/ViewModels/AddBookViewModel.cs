using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace LibraryBarcodeApp.ViewModels;

public class AddBookViewModel : INotifyPropertyChanged
{
    private string _barcode = string.Empty;
    private string _title = string.Empty;
    private string _author = string.Empty;
    private string _year = DateTime.Now.Year.ToString();
    private string _selectedCategory = "Общее";
    private string _statusMessage = "Заполните данные книги";
    private ImageSource? _barcodeImagePreview;

    public AddBookViewModel()
    {
        Categories = new ObservableCollection<string>
        {
            "Общее",
            "Художественная литература",
            "Учебная литература",
            "Наука",
            "История",
            "Техника"
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> Categories { get; }

    public string Barcode
    {
        get => _barcode;
        set
        {
            if (_barcode == value) return;
            _barcode = value;
            OnPropertyChanged();
        }
    }

    public string Title
    {
        get => _title;
        set
        {
            if (_title == value) return;
            _title = value;
            OnPropertyChanged();
        }
    }

    public string Author
    {
        get => _author;
        set
        {
            if (_author == value) return;
            _author = value;
            OnPropertyChanged();
        }
    }

    public string Year
    {
        get => _year;
        set
        {
            if (_year == value) return;
            _year = value;
            OnPropertyChanged();
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (_selectedCategory == value) return;
            _selectedCategory = value;
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage == value) return;
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public ImageSource? BarcodeImagePreview
    {
        get => _barcodeImagePreview;
        set
        {
            if (_barcodeImagePreview == value) return;
            _barcodeImagePreview = value;
            OnPropertyChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
