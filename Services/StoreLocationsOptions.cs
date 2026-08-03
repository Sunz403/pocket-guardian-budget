namespace AIShoppingAssistant.Services;

public sealed class StoreLocationsOptions
{
    public const string SectionName = "StoreLocations";
    public List<StoreLocation> Stores { get; set; } = new();
}

public sealed class StoreLocation
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Category { get; set; } = string.Empty;
}
