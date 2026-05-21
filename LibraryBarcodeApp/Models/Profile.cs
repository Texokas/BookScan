using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace LibraryBarcodeApp.Models;

[Table("profiles")]
public class Profile : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("full_name")]
    public string FullName { get; set; } = string.Empty;

    [Column("role")]
    public string Role { get; set; } = "employee";

    [Column("is_active")]
    public bool IsActive { get; set; } = true;
}
