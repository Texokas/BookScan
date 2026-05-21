using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace LibraryBarcodeApp.Models;

[Table("blocked_emails")]
public class BlockedEmail : BaseModel
{
    [PrimaryKey("email", false)]
    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Column("is_blocked")]
    public bool IsBlocked { get; set; }
}

