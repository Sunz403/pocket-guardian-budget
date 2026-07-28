namespace AIShoppingAssistant.DTOs;

public class BudgetResponseDto
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public decimal MonthlyAmount { get; set; }

    public decimal CurrentSpending { get; set; }

    public decimal RemainingAmount { get; set; }

    public int Month { get; set; }

    public int Year { get; set; }
}
