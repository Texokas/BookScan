using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace LibraryBarcodeApp.Models;

[Table("books")]
public class Book : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("barcode")]
    public string Barcode { get; set; } = string.Empty;

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("author")]
    public string Author { get; set; } = string.Empty;

    [Column("year")]
    public int Year { get; set; }

    [Column("category")]
    public string Category { get; set; } = string.Empty;

    [Column("status")]
    public string Status { get; set; } = "available";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
