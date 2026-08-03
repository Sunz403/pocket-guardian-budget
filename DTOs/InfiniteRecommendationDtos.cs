namespace AIShoppingAssistant.DTOs;

public sealed class InfiniteRecommendationResponseDto
{
    public IReadOnlyList<InfiniteRecommendedProductDto> Products { get; init; } = [];
    public bool HasMore { get; init; }
    public int TotalCount { get; init; }
    public int Page { get; init; }
}

public sealed class InfiniteRecommendedProductDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal Price { get; init; }
    public string? Color { get; init; }
    public string? Size { get; init; }
    public string StoreName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string? ImageFileName { get; init; }
    public string ImageUrl { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string ReasonType { get; init; } = string.Empty;
    public bool AddedToShoppingList { get; init; }
}
