using System.Windows;
using System.Windows.Media.Imaging;
using LibraryBarcodeApp.Services;
using System.IO;

namespace LibraryBarcodeApp;

public partial class LoginWindow : Window
{
    private readonly SupabaseService _supabaseService;
    private readonly CaptchaService _captchaService = new();
    private string _captchaText = string.Empty;

    public LoginWindow(SupabaseService supabaseService)
    {
        InitializeComponent();
        _supabaseService = supabaseService;
        Loaded += LoginWindow_Loaded;
    }

    private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= LoginWindow_Loaded;
        GenerateCaptcha();
        EmailTextBox.Focus();
    }

    private void RefreshCaptcha_Click(object sender, RoutedEventArgs e)
    {
        GenerateCaptcha();
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        ErrorLabel.Content = string.Empty;
        var email = EmailTextBox.Text.Trim();
        var password = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ErrorLabel.Content = "Введите email и пароль.";
            return;
        }

        if (!_captchaService.ValidateCaptcha(CaptchaInputTextBox.Text, _captchaText))
        {
            ErrorLabel.Content = "Неверная капча. Проверьте символы и раскладку клавиатуры.";
            GenerateCaptcha();
            return;
        }

        try
        {
            var blocked = await _supabaseService.IsBlocked(email);
            if (blocked)
            {
                await _supabaseService.RecordLoginAttempt(email, success: false);
                ErrorLabel.Content = "Вход запрещён: пользователь заблокирован.";
                GenerateCaptcha();
                return;
            }

            var response = await _supabaseService.SignIn(email, password);
            if (!response.Success)
            {
                await _supabaseService.RecordLoginAttempt(email, success: false);
                ErrorLabel.Content = response.Message;
                GenerateCaptcha();
                return;
            }

            await _supabaseService.RecordLoginAttempt(email, success: true);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            try
            {
                await _supabaseService.RecordLoginAttempt(email, success: false);
            }
            catch
            {
                // ignore
            }

            ErrorLabel.Content = ex.Message;
            GenerateCaptcha();
        }
    }

    private void DemoLogin_Click(object sender, RoutedEventArgs e)
    {
        var role = DemoAdminCheckBox.IsChecked == true ? "admin" : "employee";
        _supabaseService.LoginAsDemo(role);
        DialogResult = true;
        Close();
    }

    private void GenerateCaptcha()
    {
        _captchaText = _captchaService.GenerateCaptcha();
        CaptchaInputTextBox.Text = string.Empty;

        var png = _captchaService.GenerateCaptchaImage(_captchaText);
        using var ms = new MemoryStream(png);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = ms;
        bitmap.EndInit();
        bitmap.Freeze();
        CaptchaImage.Source = bitmap;
    }
}

