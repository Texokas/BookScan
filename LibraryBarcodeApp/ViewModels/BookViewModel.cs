using System.ComponentModel;
using System.Runtime.CompilerServices;
using LibraryBarcodeApp.Models;

namespace LibraryBarcodeApp.ViewModels;

public class BookViewModel : INotifyPropertyChanged
{
    private readonly Book _book;

    public BookViewModel(Book book)
    {
        _book = book;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid Id => _book.Id;

    public string Barcode
    {
        get => _book.Barcode;
        set
        {
            if (_book.Barcode == value) return;
            _book.Barcode = value;
            OnPropertyChanged();
        }
    }

    public string Title
    {
        get => _book.Title;
        set
        {
            if (_book.Title == value) return;
            _book.Title = value;
            OnPropertyChanged();
        }
    }

    public string Author
    {
        get => _book.Author;
        set
        {
            if (_book.Author == value) return;
            _book.Author = value;
            OnPropertyChanged();
        }
    }

    public int Year
    {
        get => _book.Year;
        set
        {
            if (_book.Year == value) return;
            _book.Year = value;
            OnPropertyChanged();
        }
    }

    public string Category
    {
        get => _book.Category;
        set
        {
            if (_book.Category == value) return;
            _book.Category = value;
            OnPropertyChanged();
        }
    }

    public string Status
    {
        get => _book.Status;
        set
        {
            if (_book.Status == value) return;
            _book.Status = value;
            OnPropertyChanged();
        }
    }

    public Book ToModel() => _book;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
