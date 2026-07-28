using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIShoppingAssistant.Models;

public class SearchHistory
{
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [Required]
    [StringLength(200)]
    public string SearchTerm { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    [Range(0, 999999.99)]
    public decimal Budget { get; set; }

    [StringLength(100)]
    public string? Location { get; set; }

    [Required]
    public DateTime SearchDate { get; set; } = DateTime.UtcNow;

    [Range(0, int.MaxValue)]
    public int ResultsCount { get; set; }
}
