namespace AIShoppingAssistant.DTOs;

public sealed class RecommendedProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal ShippingCost { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? Size { get; set; }
    public string? ImageUrl { get; set; }
    public string AiExplanation { get; set; } = string.Empty;
    public int AiScore { get; set; }
}
