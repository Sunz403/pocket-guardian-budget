using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIShoppingAssistant.Models;

public class Budget
{
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    [Range(0, 999999.99)]
    public decimal MonthlyAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Range(0, 999999.99)]
    public decimal CurrentSpending { get; set; }

    [Range(1, 12)]
    public int Month { get; set; }

    [Range(2000, 2100)]
    public int Year { get; set; }
}
