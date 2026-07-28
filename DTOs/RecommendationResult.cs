namespace AIShoppingAssistant.DTOs;

public sealed class RecommendationResult
{
    public List<ProductRecommendation> Recommendations { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

public sealed class ProductRecommendation
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int Score { get; set; }
}
