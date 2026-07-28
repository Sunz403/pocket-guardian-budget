namespace AIShoppingAssistant.DTOs;

public class ProductResponseDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public string? Color { get; set; }

    public string? Size { get; set; }

    public decimal ShippingCost { get; set; }

    public string StoreName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public DateTime CreatedAt { get; set; }
}
