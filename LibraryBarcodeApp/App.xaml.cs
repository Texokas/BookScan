using System.IO;
using System.Windows;
using System.Windows.Threading;
using LibraryBarcodeApp.Services;
using LibraryBarcodeApp.ViewModels;

namespace LibraryBarcodeApp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public SupabaseService SupabaseService { get; } = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            await SupabaseService.Initialize();
            var restored = await SupabaseService.TryRestoreSession();
            if (!restored)
            {
                var loginWindow = new global::LibraryBarcodeApp.LoginWindow(SupabaseService);
                var loginResult = loginWindow.ShowDialog();
                if (loginResult != true)
                {
                    Shutdown();
                    return;
                }
            }

            OpenMainWindow();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось подключиться к Supabase.\n{ex.Message}",
                "Ошибка подключения",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogUnhandledException(e.Exception);
        MessageBox.Show(
            $"Непредвиденная ошибка приложения.\n{e.Exception.Message}",
            "Критическая ошибка",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            LogUnhandledException(exception);
        }
    }

    private static void LogUnhandledException(Exception exception)
    {
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LibraryBarcodeApp");
            Directory.CreateDirectory(folder);
            var filePath = Path.Combine(folder, "crash.log");
            var payload = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}\n\n";
            File.AppendAllText(filePath, payload);
        }
        catch
        {
            // Ignore logging failures.
        }
    }

    private void OpenMainWindow()
    {
        var mainWindowViewModel = new MainWindowViewModel(SupabaseService);
        var mainWindow = new MainWindow(mainWindowViewModel);
        var currentUser = SupabaseService.GetCurrentUser();
        if (currentUser is not null)
        {
            mainWindow.Title = $"Library Barcode App - {currentUser.Role}";
        }

        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        mainWindow.Show();
    }

    public async Task HandleLogoutAsync(Window currentWindow)
    {
        await SupabaseService.SignOut();

        var loginWindow = new global::LibraryBarcodeApp.LoginWindow(SupabaseService);
        var loginResult = loginWindow.ShowDialog();

        if (loginResult == true)
        {
            OpenMainWindow();
            currentWindow.Close();
            return;
        }

        Shutdown();
    }
}

