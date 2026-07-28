using System.ComponentModel.DataAnnotations;

namespace AIShoppingAssistant.DTOs;

public sealed class AddToCartDto
{
    [Required]
    [Range(1, int.MaxValue)]
    public int ProductId { get; init; }

    [Required]
    [Range(1, 999)]
    public int Quantity { get; init; }
}

public sealed class CartItemDto
{
    public int Id { get; init; }
    public int ProductId { get; init; }

    [Required]
    [StringLength(150)]
    public string ProductName { get; init; } = string.Empty;

    [Range(typeof(decimal), "0", "999999.99")]
    public decimal Price { get; init; }

    [Range(1, 999)]
    public int Quantity { get; init; }

    [Range(typeof(decimal), "0", "999999999.99")]
    public decimal LineTotal { get; init; }

    public DateTime AddedDate { get; init; }
}

public sealed class CartSummaryDto
{
    public IReadOnlyList<CartItemDto> Items { get; init; } = [];

    [Range(0, int.MaxValue)]
    public int ItemCount { get; init; }

    [Range(typeof(decimal), "0", "999999999.99")]
    public decimal TotalPrice { get; init; }
}

public class CheckoutResponseDto
{
    public int PurchaseHistoryId { get; init; }
    public DateTime PurchaseDate { get; init; }

    [Required]
    public CartSummaryDto Order { get; init; } = new();

    [Range(typeof(decimal), "0", "999999.99")]
    public decimal RemainingBudget { get; init; }

    [Required]
    [StringLength(500)]
    public string Message { get; init; } = string.Empty;
}

public sealed class CheckoutConfirmationDto : CheckoutResponseDto;
