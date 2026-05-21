using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using LibraryBarcodeApp.Commands;
using LibraryBarcodeApp.Models;
using LibraryBarcodeApp.Services;

namespace LibraryBarcodeApp.ViewModels;

public sealed class UsersWindowViewModel : INotifyPropertyChanged
{
    private readonly SupabaseService _supabaseService;
    private string _searchText = string.Empty;
    private string _statusMessage = "Готово";
    private string _statisticsText = "Админов: 0 | Сотрудников: 0";
    private User? _selectedUser;

    public UsersWindowViewModel(SupabaseService supabaseService)
    {
        _supabaseService = supabaseService;
        Users = new ObservableCollection<User>();
        UsersView = CollectionViewSource.GetDefaultView(Users);
        UsersView.Filter = FilterUser;

        RefreshCommand = new RelayCommand(async _ => await LoadUsersAsync());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<User> Users { get; }

    public ICollectionView UsersView { get; }

    public ICommand RefreshCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            OnPropertyChanged();
            UsersView.Refresh();
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

    public User? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (_selectedUser == value) return;
            _selectedUser = value;
            OnPropertyChanged();
        }
    }

    public async Task InitializeAsync()
    {
        await LoadUsersAsync();
    }

    public async Task LoadUsersAsync()
    {
        try
        {
            StatusMessage = "Загрузка пользователей...";
            var users = await _supabaseService.GetAllUsers();

            Users.Clear();
            foreach (var user in users.OrderBy(u => u.Email).ThenBy(u => u.FullName))
            {
                Users.Add(user);
            }

            RefreshStatistics();
            UsersView.Refresh();
            StatusMessage = $"Загружено пользователей: {Users.Count}";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    public async Task ApplySelectedRoleAsync()
    {
        if (SelectedUser is null) return;
        StatusMessage = "Сохранение роли...";
        await _supabaseService.UpdateUserRole(SelectedUser.Id, SelectedUser.Role);
        RefreshStatistics();
        StatusMessage = "Роль обновлена.";
    }

    public async Task ApplySelectedBlockedAsync(bool isBlocked)
    {
        if (SelectedUser is null) return;
        StatusMessage = "Сохранение статуса...";
        await _supabaseService.SetUserActive(SelectedUser.Id, isActive: !isBlocked);
        SelectedUser.IsActive = !isBlocked;
        StatusMessage = isBlocked ? "Пользователь заблокирован." : "Пользователь активен.";
    }

    public async Task DeleteSelectedAsync()
    {
        if (SelectedUser is null) return;
        StatusMessage = "Удаление пользователя...";
        await _supabaseService.DeleteUser(SelectedUser.Id);
        Users.Remove(SelectedUser);
        SelectedUser = null;
        RefreshStatistics();
        StatusMessage = "Пользователь удалён.";
    }

    public async Task<string> ResetSelectedPasswordAsync()
    {
        if (SelectedUser is null) return string.Empty;
        var newPassword = GeneratePassword(12);
        StatusMessage = "Сброс пароля...";
        await _supabaseService.ResetUserPassword(SelectedUser.Id, newPassword);
        StatusMessage = "Пароль сброшен.";
        return newPassword;
    }

    public async Task AddUserByAdminAsync(string email, string password, string fullName, string role)
    {
        StatusMessage = "Создание пользователя...";
        await _supabaseService.CreateUserByAdmin(email, password, fullName, role);
        await LoadUsersAsync();
        StatusMessage = "Пользователь создан.";
    }

    private bool FilterUser(object obj)
    {
        if (obj is not User user)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        return user.Email.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
               || user.FullName.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshStatistics()
    {
        var admins = Users.Count(u => string.Equals(u.Role, "admin", StringComparison.OrdinalIgnoreCase));
        var employees = Users.Count - admins;
        StatisticsText = $"Админов: {admins} | Сотрудников: {employees}";
    }

    private static string GeneratePassword(int length)
    {
        const string chars = "ABCDEFGHJKMNPQRTUVWXYabcdefghjkmnpqrtuvwxy23456789!@#$%";
        var rnd = new Random();
        var sb = new System.Text.StringBuilder(length);
        for (var i = 0; i < length; i++)
        {
            sb.Append(chars[rnd.Next(chars.Length)]);
        }

        return sb.ToString();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
