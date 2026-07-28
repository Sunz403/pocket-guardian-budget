using System.ComponentModel.DataAnnotations;

namespace AIShoppingAssistant.DTOs;

public sealed class BudgetSummaryDto
{
    [Range(typeof(decimal), "0", "999999.99")]
    public decimal MonthlyAmount { get; set; }

    [Range(typeof(decimal), "0", "999999.99")]
    public decimal BudgetAmount { get; set; }

    [Range(typeof(decimal), "0", "999999.99")]
    public decimal CurrentSpending { get; set; }

    [Range(typeof(decimal), "0", "999999.99")]
    public decimal RemainingAmount { get; set; }

    [Range(typeof(decimal), "0", "100")]
    public decimal PercentageUsed { get; set; }
}
