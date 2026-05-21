using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace LibraryBarcodeApp.Models;

[Table("operations")]
public class Operation : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("book_id")]
    public Guid BookId { get; set; }

    [Column("operation_type")]
    public string OperationType { get; set; } = string.Empty;

    [Column("timestamp")]
    public DateTime Timestamp { get; set; }
}
