using System.Windows;
using System.Windows.Controls;

namespace LibraryBarcodeApp;

public partial class AddUserDialog : Window
{
    public AddUserDialog()
    {
        InitializeComponent();
        Loaded += AddUserDialog_Loaded;
    }

    public string Email { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public string Password { get; private set; } = string.Empty;

    public string Role { get; private set; } = "employee";

    private void AddUserDialog_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= AddUserDialog_Loaded;
        EmailTextBox.Focus();
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        ErrorLabel.Content = string.Empty;

        var email = EmailTextBox.Text.Trim();
        var fullName = FullNameTextBox.Text.Trim();
        var password = PasswordBox.Password;
        var confirm = ConfirmPasswordBox.Password;
        var role = (RoleComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "employee";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(fullName)
            || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(confirm))
        {
            ErrorLabel.Content = "Заполните все поля.";
            return;
        }

        if (!email.Contains('@') || !email.Contains('.'))
        {
            ErrorLabel.Content = "Некорректный Email.";
            return;
        }

        if (!string.Equals(password, confirm, StringComparison.Ordinal))
        {
            ErrorLabel.Content = "Пароль и подтверждение не совпадают.";
            return;
        }

        if (password.Length < 6)
        {
            ErrorLabel.Content = "Пароль слишком короткий (минимум 6 символов).";
            return;
        }

        Email = email;
        FullName = fullName;
        Password = password;
        Role = role;

        DialogResult = true;
        Close();
    }
}

