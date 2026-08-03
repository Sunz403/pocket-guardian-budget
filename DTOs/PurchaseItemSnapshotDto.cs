using System.ComponentModel.DataAnnotations;

namespace AIShoppingAssistant.DTOs;

// A historical purchase snapshot retained only for budget reporting and recommendations.
public sealed class PurchaseItemSnapshotDto
{
    public int ProductId { get; init; }
    [Required, StringLength(150)] public string ProductName { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public int Quantity { get; init; }
    public decimal LineTotal { get; init; }
    public DateTime AddedDate { get; init; }
}
