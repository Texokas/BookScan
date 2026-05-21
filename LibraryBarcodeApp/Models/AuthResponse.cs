namespace LibraryBarcodeApp.Models;

public class AuthResponse
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public User? User { get; set; }

    public string AccessToken { get; set; } = string.Empty;
}
