using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace LibraryBarcodeApp.Models;

[Table("login_attempts")]
public class LoginAttempt : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Column("success")]
    public bool Success { get; set; }

    [Column("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

