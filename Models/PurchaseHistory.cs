using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIShoppingAssistant.Models;

public class PurchaseHistory
{
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [Required]
    public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "decimal(18,2)")]
    [Range(0, 999999.99)]
    public decimal TotalAmount { get; set; }

    // Immutable JSON snapshot of the purchased line items.
    [Required]
    public string Items { get; set; } = "[]";
}
