using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using LibraryBarcodeApp.Commands;
using LibraryBarcodeApp.Models;
using LibraryBarcodeApp.Services;

namespace LibraryBarcodeApp.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly SupabaseService _supabaseService;
    private readonly RelayCommand _borrowCommand;
    private readonly RelayCommand _returnCommand;
    private string _searchText = string.Empty;
    private string _statisticsText = "Всего книг: 0 | Выдано: 0 | Доступно: 0";
    private string _statusMessage = "Готово";
    private BookViewModel? _selectedBook;
    private string _currentRole = "employee";
    private string _currentEmail = string.Empty;

    public MainWindowViewModel(SupabaseService supabaseService)
    {
        _supabaseService = supabaseService;

        Books = new ObservableCollection<BookViewModel>();
        BooksView = CollectionViewSource.GetDefaultView(Books);
        BooksView.Filter = FilterBook;

        _borrowCommand = new RelayCommand(async _ => await BorrowSelectedBookAsync(), _ => CanBorrowSelectedBook());
        _returnCommand = new RelayCommand(async _ => await ReturnSelectedBookAsync(), _ => CanReturnSelectedBook());
        RefreshCommand = new RelayCommand(async _ => await LoadBooksAsync());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<BookViewModel> Books { get; }

    public ICollectionView BooksView { get; }

    public ICommand BorrowCommand => _borrowCommand;

    public ICommand ReturnCommand => _returnCommand;

    public ICommand RefreshCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            OnPropertyChanged();
            BooksView.Refresh();
        }
    }

    public string StatisticsText
    {
        get => _statisticsText;
        private set
        {
            if (_statisticsText == value) return;
            _statisticsText = value;
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value) return;
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public string CurrentRole
    {
        get => _currentRole;
        private set
        {
            if (_currentRole == value) return;
            _currentRole = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAdmin));
            OnPropertyChanged(nameof(UserStatusText));
        }
    }

    public string CurrentEmail
    {
        get => _currentEmail;
        private set
        {
            if (_currentEmail == value) return;
            _currentEmail = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UserStatusText));
        }
    }

    public bool IsAdmin =>
        string.Equals(CurrentRole, "admin", StringComparison.OrdinalIgnoreCase);

    public string UserStatusText =>
        $"Email: {(string.IsNullOrWhiteSpace(CurrentEmail) ? "—" : CurrentEmail)} | Роль: {(IsAdmin ? "Администратор" : "Сотрудник")}";

    public BookViewModel? SelectedBook
    {
        get => _selectedBook;
        set
        {
            if (_selectedBook == value) return;
            _selectedBook = value;
            OnPropertyChanged();
            _borrowCommand.RaiseCanExecuteChanged();
            _returnCommand.RaiseCanExecuteChanged();
        }
    }

    public void SetRole(string? role)
    {
        CurrentRole = string.IsNullOrWhiteSpace(role) ? "employee" : role.Trim();
    }

    public void SetUserContext(string? email, string? role)
    {
        CurrentEmail = string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim();
        SetRole(role);
    }

    public async Task InitializeAsync()
    {
        await LoadBooksAsync();
    }

    public async Task LoadBooksAsync()
    {
        try
        {
            StatusMessage = "Загрузка книг из Supabase...";
            var books = await _supabaseService.GetBooks();

            Books.Clear();
            foreach (var book in books)
            {
                Books.Add(new BookViewModel(book));
            }

            RefreshStatistics();
            BooksView.Refresh();
            StatusMessage = $"Подключено к Supabase. Загружено книг: {Books.Count}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки: {ex.Message}";
        }
    }

    private async Task BorrowSelectedBookAsync()
    {
        if (SelectedBook is null) return;

        try
        {
            await _supabaseService.UpdateBookStatus(SelectedBook.Id, "borrowed");
            await _supabaseService.AddOperation(new Operation
            {
                Id = Guid.NewGuid(),
                BookId = SelectedBook.Id,
                OperationType = "borrow",
                Timestamp = DateTime.UtcNow
            });

            await LoadBooksAsync();
            StatusMessage = "Книга выдана.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка выдачи: {ex.Message}";
        }
    }

    private async Task ReturnSelectedBookAsync()
    {
        if (SelectedBook is null) return;

        try
        {
            await _supabaseService.UpdateBookStatus(SelectedBook.Id, "available");
            await _supabaseService.AddOperation(new Operation
            {
                Id = Guid.NewGuid(),
                BookId = SelectedBook.Id,
                OperationType = "return",
                Timestamp = DateTime.UtcNow
            });

            await LoadBooksAsync();
            StatusMessage = "Книга возвращена.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка возврата: {ex.Message}";
        }
    }

    private bool CanBorrowSelectedBook()
    {
        return SelectedBook is not null && SelectedBook.Status == "available";
    }

    private bool CanReturnSelectedBook()
    {
        return SelectedBook is not null && SelectedBook.Status == "borrowed";
    }

    private bool FilterBook(object obj)
    {
        if (obj is not BookViewModel book)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        return book.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
               || book.Author.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
               || book.Barcode.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshStatistics()
    {
        var total = Books.Count;
        var borrowed = Books.Count(b => b.Status == "borrowed");
        var available = Books.Count(b => b.Status == "available");
        StatisticsText = $"Всего книг: {total} | Выдано: {borrowed} | Доступно: {available}";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
