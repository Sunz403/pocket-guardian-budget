using System.ComponentModel.DataAnnotations;

namespace AIShoppingAssistant.DTOs;

public sealed class ShoppingQueryDto
{
    [Required]
    [StringLength(1000, MinimumLength = 2)]
    public string UserQuery { get; init; } = string.Empty;

    [Range(typeof(decimal), "0", "999999.99")]
    public decimal? Budget { get; init; }

    [StringLength(200)]
    public string? Location { get; init; }

    [Range(1, 500)]
    public int? MaxDistance { get; init; }
}

public sealed class ShoppingResponseDto
{
    public IReadOnlyList<ProductDto> RecommendedProducts { get; init; } = [];

    [Required]
    [StringLength(4000)]
    public string AiExplanation { get; init; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int TotalMatchingProducts { get; init; }

    [Required]
    public BudgetSummaryDto BudgetSummary { get; init; } = new();

    [Required]
    public ParsedQuery ParsedQuery { get; init; } = new();
}

public sealed class ProductDto
{
    public int Id { get; init; }

    [Required]
    [StringLength(150)]
    public string Name { get; init; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; init; }

    [Range(typeof(decimal), "0", "999999.99")]
    public decimal Price { get; init; }

    [StringLength(50)]
    public string? Color { get; init; }

    [StringLength(50)]
    public string? Size { get; init; }

    [Range(typeof(decimal), "0", "999999.99")]
    public decimal ShippingCost { get; init; }

    [Required]
    [StringLength(100)]
    public string StoreName { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Category { get; init; } = string.Empty;

    [Url]
    [StringLength(500)]
    public string? ImageUrl { get; init; }

    public DateTime CreatedAt { get; init; }

    [StringLength(1000)]
    public string AiReason { get; init; } = string.Empty;
}
