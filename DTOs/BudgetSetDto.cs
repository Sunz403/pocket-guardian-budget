using System.ComponentModel.DataAnnotations;

namespace AIShoppingAssistant.DTOs;

public class BudgetSetDto
{
    [Range(0, 999999.99)]
    public decimal MonthlyAmount { get; set; }

    [Range(0, 999999.99)]
    public decimal? CurrentSpending { get; set; }

    [Range(1, 12)]
    public int? Month { get; set; }

    [Range(2000, 2100)]
    public int? Year { get; set; }
}
