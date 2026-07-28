namespace AIShoppingAssistant.DTOs;

public class UserPreferenceDto
{
    public List<string> FavoriteStyles { get; set; } = new();

    public List<string> FavoriteColors { get; set; } = new();

    public List<string> FavoriteStores { get; set; } = new();

    public decimal PreferredPriceRangeMin { get; set; }

    public decimal PreferredPriceRangeMax { get; set; }
}
