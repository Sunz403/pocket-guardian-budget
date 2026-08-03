using AIShoppingAssistant.DTOs;
using AIShoppingAssistant.Data;
using AIShoppingAssistant.Models;
using Microsoft.EntityFrameworkCore;

namespace AIShoppingAssistant.Services;

public sealed class StoreService
{
    private const double EarthRadiusKm = 6371.0088;
    private readonly ApplicationDbContext _context;

    public StoreService(ApplicationDbContext context) => _context = context;

    public async Task<List<Store>> GetNearbyStoresAsync(GeoCoordinates origin, double radiusKm, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(radiusKm);
        var stores = await _context.Stores.AsNoTracking().ToListAsync(cancellationToken);
        return stores.Where(store => CalculateDistanceInKilometers(origin, ToCoordinates(store)) <= radiusKm)
            .OrderBy(store => CalculateDistanceInKilometers(origin, ToCoordinates(store))).ToList();
    }

    public IReadOnlyList<StoreDistance> GetNearbyStores(GeoCoordinates origin, double radiusKm, string? category = null) =>
        _context.Stores.AsNoTracking().AsEnumerable()
            .Where(s => string.IsNullOrWhiteSpace(category) || s.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .Select(s => new StoreDistance(s, CalculateDistanceInKilometers(origin, ToCoordinates(s))))
            .Where(s => s.DistanceKm <= radiusKm)
            .OrderBy(s => s.DistanceKm)
            .ToList();

    public StoreDistance? FindStore(string storeName, GeoCoordinates origin) =>
        _context.Stores.AsNoTracking().AsEnumerable()
            .Where(s => NamesMatch(storeName, s.Name))
            .Select(s => new StoreDistance(s, CalculateDistanceInKilometers(origin, ToCoordinates(s))))
            .OrderBy(s => s.DistanceKm)
            .FirstOrDefault();

    public IReadOnlyList<Store> GetStores(string? category = null) => _context.Stores.AsNoTracking()
        .Where(s => string.IsNullOrWhiteSpace(category) || s.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
        .OrderBy(s => s.Name).ToList();

    public IReadOnlyList<string> Categories => _context.Stores.AsNoTracking().Select(s => s.Category).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().Order().ToList();

    public static decimal CalculateDeliveryCost(double distanceKm) => distanceKm switch
    {
        <= 5 => 25m,
        <= 10 => 40m,
        <= 25 => 65m,
        <= 50 => 95m,
        _ => 140m
    };

    public static double CalculateDistanceInKilometers(GeoCoordinates first, GeoCoordinates second)
    {
        var lat = ToRadians(second.Latitude - first.Latitude);
        var lng = ToRadians(second.Longitude - first.Longitude);
        var a = Math.Pow(Math.Sin(lat / 2), 2) + Math.Cos(ToRadians(first.Latitude)) * Math.Cos(ToRadians(second.Latitude)) * Math.Pow(Math.Sin(lng / 2), 2);
        return EarthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static GeoCoordinates ToCoordinates(Store store) => new() { Latitude = store.Latitude, Longitude = store.Longitude };
    private static double ToRadians(double value) => value * Math.PI / 180d;
    private static bool NamesMatch(string productStore, string configuredStore) =>
        configuredStore.Contains(productStore, StringComparison.OrdinalIgnoreCase) || productStore.Contains(configuredStore, StringComparison.OrdinalIgnoreCase);
}

public sealed record StoreDistance(Store Store, double DistanceKm)
{
    public decimal DeliveryCost => StoreService.CalculateDeliveryCost(DistanceKm);
}
