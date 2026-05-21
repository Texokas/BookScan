using System.Windows;
using LibraryBarcodeApp.Models;
using LibraryBarcodeApp.Services;
using LibraryBarcodeApp.ViewModels;

namespace LibraryBarcodeApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly BarcodeScanService _barcodeScanService = new();
    private ProfileWindow? _profileWindow;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        if (Application.Current is App app)
        {
            var currentUser = app.SupabaseService.GetCurrentUser();
            _viewModel.SetUserContext(currentUser?.Email, currentUser?.Role);
        }

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;
        if (Application.Current is App app)
        {
            var currentUser = app.SupabaseService.GetCurrentUser();
            _viewModel.SetUserContext(currentUser?.Email, currentUser?.Role);
        }
        await _viewModel.InitializeAsync();
    }

    private async void AddBook_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current is not App app)
        {
            return;
        }

        var addBookWindow = new AddBookWindow(app.SupabaseService)
        {
            Owner = this
        };

        var result = addBookWindow.ShowDialog();
        if (result == true)
        {
            await _viewModel.LoadBooksAsync();
        }
    }

    private async void Borrow_Click(object sender, RoutedEventArgs e)
    {
        await ProcessBorrowReturnAsync(isBorrowOperation: true);
    }

    private async void Return_Click(object sender, RoutedEventArgs e)
    {
        await ProcessBorrowReturnAsync(isBorrowOperation: false);
    }

    private async Task ProcessBorrowReturnAsync(bool isBorrowOperation)
    {
        if (Application.Current is not App app)
        {
            return;
        }

        var actionTitle = isBorrowOperation ? "Выдача книги" : "Возврат книги";

        var sourceDialog = new ScanSourceDialog { Owner = this };
        if (sourceDialog.ShowDialog() != true)
            return;

        var barcode = sourceDialog.SelectedSource switch
        {
            ScanSource.Camera => _barcodeScanService.ScanBarcodeFromCamera(this),
            ScanSource.Image  => _barcodeScanService.ScanBarcodeFromImage(),
            _ => null
        };

        if (barcode is null)
        {
            MessageBox.Show(
                "Сканирование отменено или штрихкод не распознан.",
                actionTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(barcode))
        {
            MessageBox.Show(
                "Не удалось распознать штрихкод.",
                actionTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            var book = await app.SupabaseService.GetBookByBarcode(barcode);
            if (book is null)
            {
                MessageBox.Show(
                    $"Книга со штрихкодом {barcode} не найдена.",
                    actionTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (isBorrowOperation)
            {
                if (!string.Equals(book.Status, "available", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        $"Книга уже недоступна для выдачи. Текущий статус: {book.Status}.",
                        actionTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                await app.SupabaseService.UpdateBookStatus(book.Id, "borrowed");
                await app.SupabaseService.AddOperation(new Operation
                {
                    Id = Guid.NewGuid(),
                    BookId = book.Id,
                    OperationType = "borrow",
                    Timestamp = DateTime.UtcNow
                });

                await _viewModel.LoadBooksAsync();
                MessageBox.Show(
                    $"Книга \"{book.Title}\" успешно выдана.",
                    actionTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                if (!string.Equals(book.Status, "borrowed", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        $"Книга не находится в статусе выданной. Текущий статус: {book.Status}.",
                        actionTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                await app.SupabaseService.UpdateBookStatus(book.Id, "available");
                await app.SupabaseService.AddOperation(new Operation
                {
                    Id = Guid.NewGuid(),
                    BookId = book.Id,
                    OperationType = "return",
                    Timestamp = DateTime.UtcNow
                });

                await _viewModel.LoadBooksAsync();
                MessageBox.Show(
                    $"Книга \"{book.Title}\" успешно возвращена.",
                    actionTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка при выполнении операции.\n{ex.Message}",
                actionTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OpenReports_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current is not App app)
        {
            return;
        }

        var reportsWindow = new ReportsWindow(app.SupabaseService)
        {
            Owner = this
        };

        reportsWindow.ShowDialog();
    }

    private async void Logout_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current is not App app)
        {
            return;
        }

        try
        {
            await app.HandleLogoutAsync(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка выхода из аккаунта.\n{ex.Message}",
                "Выход",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void DeleteBook_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current is not App app)
        {
            return;
        }

        if (!_viewModel.IsAdmin)
        {
            MessageBox.Show(
                "Доступ запрещён: требуется роль администратора.",
                "Удалить книгу",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (_viewModel.SelectedBook is null)
        {
            MessageBox.Show(
                "Сначала выберите книгу в таблице.",
                "Удалить книгу",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var selectedBook = _viewModel.SelectedBook;
        var confirmation = MessageBox.Show(
            $"Удалить книгу \"{selectedBook.Title}\"?\nЭто действие нельзя отменить.",
            "Подтверждение удаления",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await app.SupabaseService.DeleteBook(selectedBook.Id);
            await _viewModel.LoadBooksAsync();
            MessageBox.Show(
                "Книга успешно удалена.",
                "Удалить книгу",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка при удалении книги.\n{ex.Message}",
                "Удалить книгу",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void EditBook_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Функция редактирования книги доступна только администратору и будет реализована позже.",
            "Редактировать книгу",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void MarkLost_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Функция смены статуса на 'Утеряна' доступна только администратору и будет реализована позже.",
            "Статус: Утеряна",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OpenUsers_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current is not App app)
        {
            return;
        }

        if (!_viewModel.IsAdmin)
        {
            MessageBox.Show(
                "Доступ запрещён: требуется роль администратора.",
                "Пользователи",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var usersWindow = new global::LibraryBarcodeApp.UsersWindow(app.SupabaseService)
        {
            Owner = this
        };
        usersWindow.ShowDialog();
    }

    private void OpenAdminStatistics_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Раздел 'Статистика админа' доступен только администратору и будет реализован позже.",
            "Статистика админа",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OpenMyProfile_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current is not App app)
        {
            return;
        }

        var currentUser = app.SupabaseService.GetCurrentUser();
        if (currentUser is null)
        {
            MessageBox.Show(
                "Сессия не найдена. Выполните вход снова.",
                "Мой профиль",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (_profileWindow is not null)
            {
                _profileWindow.Activate();
                return;
            }

            _profileWindow = new global::LibraryBarcodeApp.ProfileWindow(app.SupabaseService)
            {
                Owner = this
            };
            _profileWindow.Closed += (_, _) => _profileWindow = null;
            _profileWindow.Show();

            currentUser = app.SupabaseService.GetCurrentUser();
            _viewModel.SetUserContext(currentUser?.Email, currentUser?.Role);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось открыть профиль.\n{ex.Message}",
                "Мой профиль",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OpenAuditLog_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Раздел 'Журнал аудита' доступен только администратору и будет реализован позже.",
            "Журнал аудита",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OpenLoginAttempts_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Журнал попыток входа будет реализован позже.",
            "Журнал попыток входа",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OpenAbout_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "LibraryBarcodeApp\nУчет книг библиотеки.",
            "О программе",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}