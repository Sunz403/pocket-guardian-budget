using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIShoppingAssistant.Models;

public class CartItem
{
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [Required]
    public int ProductId { get; set; }

    [Required, StringLength(150)]
    public string ProductName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    [Range(0, 999999.99)]
    public decimal Price { get; set; }

    [Range(1, 999)]
    public int Quantity { get; set; }

    [Required]
    public DateTime AddedDate { get; set; } = DateTime.UtcNow;
}
