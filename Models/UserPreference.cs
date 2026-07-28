using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIShoppingAssistant.Models;

public class UserPreference
{
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public List<string> FavoriteStyles { get; set; } = new();

    public List<string> FavoriteColors { get; set; } = new();

    public List<string> FavoriteStores { get; set; } = new();

    [Column(TypeName = "decimal(18,2)")]
    [Range(0, 999999.99)]
    public decimal PreferredPriceRangeMin { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Range(0, 999999.99)]
    public decimal PreferredPriceRangeMax { get; set; }
}
