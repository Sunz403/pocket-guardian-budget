using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIShoppingAssistant.Models;

public class Product
{
    public int Id { get; set; }

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    [Range(0, 999999.99)]
    public decimal Price { get; set; }

    [StringLength(50)]
    public string? Color { get; set; }

    [StringLength(50)]
    public string? Size { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Range(0, 999999.99)]
    public decimal ShippingCost { get; set; }

    [Required]
    [StringLength(100)]
    public string StoreName { get; set; } = string.Empty;

    [StringLength(2048)]
    [RegularExpression(@"^https?://.+", ErrorMessage = "Store URL must start with http:// or https://.")]
    public string? StoreUrl { get; set; }

    // StoreName is retained for existing catalog data; StoreId enables geographic searches.
    public int? StoreId { get; set; }

    public Store? Store { get; set; }

    [Required]
    [StringLength(100)]
    public string Category { get; set; } = string.Empty;

    [StringLength(255)]
    public string? ImageFileName { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
