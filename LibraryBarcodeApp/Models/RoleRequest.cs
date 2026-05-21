using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace LibraryBarcodeApp.Models;

[Table("role_requests")]
public class RoleRequest : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("requested_role")]
    public string RequestedRole { get; set; } = "admin";

    [Column("status")]
    public string Status { get; set; } = "pending";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("message")]
    public string Message { get; set; } = string.Empty;
}

