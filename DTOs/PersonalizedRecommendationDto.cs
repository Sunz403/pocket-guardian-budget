namespace AIShoppingAssistant.DTOs;

public sealed class PersonalizedRecommendationDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal Price { get; init; }
    public string? Color { get; init; }
    public string? Size { get; init; }
    public string StoreName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string ReasonType { get; init; } = string.Empty;
    public int Score { get; init; }
}
