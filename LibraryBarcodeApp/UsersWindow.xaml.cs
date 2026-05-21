using System.Windows;
using LibraryBarcodeApp.Services;
using LibraryBarcodeApp.ViewModels;

namespace LibraryBarcodeApp;

public partial class UsersWindow : Window
{
    private readonly UsersWindowViewModel _viewModel;

    public UsersWindow(SupabaseService supabaseService)
    {
        InitializeComponent();
        _viewModel = new UsersWindowViewModel(supabaseService);
        DataContext = _viewModel;
        Loaded += UsersWindow_Loaded;
    }

    private async void UsersWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= UsersWindow_Loaded;
        await _viewModel.InitializeAsync();
    }

    private async void AddUser_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new global::LibraryBarcodeApp.AddUserDialog { Owner = this };
            var result = dialog.ShowDialog();
            if (result != true)
            {
                return;
            }

            await _viewModel.AddUserByAdminAsync(dialog.Email, dialog.Password, dialog.FullName, dialog.Role);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Пользователи", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void DeleteUser_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedUser is null)
        {
            MessageBox.Show("Выберите пользователя.", "Пользователи", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"Удалить пользователя {_viewModel.SelectedUser.Email}?",
            "Удаление",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _viewModel.DeleteSelectedAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Пользователи", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ApplyRole_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedUser is null)
        {
            MessageBox.Show("Выберите пользователя.", "Пользователи", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            await _viewModel.ApplySelectedRoleAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Пользователи", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ApplyBlock_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedUser is null)
        {
            MessageBox.Show("Выберите пользователя.", "Пользователи", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var isBlocked = !_viewModel.SelectedUser.IsActive;
            await _viewModel.ApplySelectedBlockedAsync(isBlocked);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Пользователи", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ResetPassword_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedUser is null)
        {
            MessageBox.Show("Выберите пользователя.", "Пользователи", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var newPassword = await _viewModel.ResetSelectedPasswordAsync();
            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                MessageBox.Show(
                    $"Новый пароль для {_viewModel.SelectedUser.Email}:\n{newPassword}",
                    "Сброс пароля",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Пользователи", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

