using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using LibraryBarcodeApp.Services;

namespace LibraryBarcodeApp;

public partial class ProfileWindow : Window, INotifyPropertyChanged
{
    private readonly SupabaseService _supabaseService;
    private string _email = string.Empty;
    private string _fullName = string.Empty;
    private string _statusMessage = "Готово";
    private bool _isAdmin;

    public ProfileWindow(SupabaseService supabaseService)
    {
        InitializeComponent();
        _supabaseService = supabaseService;
        DataContext = this;

        var user = _supabaseService.GetCurrentUser();
        if (user is null)
        {
            StatusMessage = "Сессия не найдена. Выполните вход снова.";
            Email = "Не авторизован";
            FullName = string.Empty;
            RequestUpgradeButton.Visibility = Visibility.Collapsed;
            return;
        }

        Email = user.Email;
        FullName = user.FullName;
        _isAdmin = string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase);
        RequestUpgradeButton.Visibility = _isAdmin ? Visibility.Collapsed : Visibility.Visible;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Email
    {
        get => _email;
        private set
        {
            if (_email == value) return;
            _email = value;
            OnPropertyChanged();
        }
    }

    public string FullName
    {
        get => _fullName;
        set
        {
            if (_fullName == value) return;
            _fullName = value;
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

    private void SaveFullName_Click(object sender, RoutedEventArgs e)
    {
        StatusMessage = "Изменение ФИО будет реализовано позже.";
        MessageBox.Show(StatusMessage, "Профиль", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        StatusMessage = "Смена пароля будет реализована позже.";
        MessageBox.Show(StatusMessage, "Смена пароля", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RequestUpgrade_Click(object sender, RoutedEventArgs e)
    {
        StatusMessage = "Запрос повышения роли будет реализован позже.";
        MessageBox.Show(StatusMessage, "Запрос роли", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

