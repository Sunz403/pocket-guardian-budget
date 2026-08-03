using System.Text.Json;
using System.Text.Json.Serialization;
using AIShoppingAssistant.DTOs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AIShoppingAssistant.Services;

public sealed class LocationService
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(7);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OpenCageOptions _openCageOptions;
    private readonly StoreService _storeService;
    private readonly ILogger<LocationService> _logger;
    private readonly IMemoryCache _cache;

    public LocationService(
        IHttpClientFactory httpClientFactory,
        IOptions<OpenCageOptions> openCageOptions,
        StoreService storeService,
        ILogger<LocationService> logger,
        IMemoryCache cache)
    {
        _httpClientFactory = httpClientFactory;
        _openCageOptions = openCageOptions.Value;
        _storeService = storeService;
        _logger = logger;
        _cache = cache;
    }

    public async Task<GeoCoordinates?> GeocodeAsync(
        string location,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidLocation(location)) return null;
        var normalized = location.Trim().ToUpperInvariant();
        if (_cache.TryGetValue<GeoCoordinates>(CacheKey(normalized), out var cached)) return cached;
        if (string.IsNullOrWhiteSpace(_openCageOptions.ApiKey))
        {
            _logger.LogError("OpenCage API key is not configured.");
            return null;
        }

        var client = _httpClientFactory.CreateClient("OpenCage");
        // Postal codes are ambiguous globally, so use the application's market.
        var query = $"{location.Trim()}, South Africa";
        var requestUri = $"geocode/v1/json?q={Uri.EscapeDataString(query)}&key={Uri.EscapeDataString(_openCageOptions.ApiKey)}&limit=1&no_annotations=1";
        try
        {
            using var response = await client.GetAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenCage postal-code lookup returned HTTP {StatusCode}.", response.StatusCode);
                return null;
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var result = await JsonSerializer.DeserializeAsync<OpenCageResponse>(responseStream, cancellationToken: cancellationToken);
            var geometry = result?.Results?.FirstOrDefault()?.Geometry;
            if (geometry is null || geometry.Latitude is < -90 or > 90 || geometry.Longitude is < -180 or > 180) return null;
            var coordinates = new GeoCoordinates { Latitude = geometry.Latitude, Longitude = geometry.Longitude };
            _cache.Set(CacheKey(normalized), coordinates, CacheLifetime);
            return coordinates;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "OpenCage could not geocode location {Location}.", location);
            return null;
        }
    }

    public Task<GeoCoordinates?> ValidatePostalCodeAsync(string postalCode, CancellationToken cancellationToken = default) =>
        GeocodeAsync(postalCode, cancellationToken);

    public static bool IsValidLocation(string? location) =>
        !string.IsNullOrWhiteSpace(location) && location.Trim().Length is >= 3 and <= 200;

    public async Task<double?> CalculateDistanceAsync(
        string firstPostalCode,
        string secondPostalCode,
        CancellationToken cancellationToken = default)
    {
        var first = await GeocodeAsync(firstPostalCode, cancellationToken);
        var second = await GeocodeAsync(secondPostalCode, cancellationToken);
        return first is null || second is null ? null : StoreService.CalculateDistanceInKilometers(first, second);
    }

    public async Task<List<string>> GetNearbyStoresAsync(
        string postalCode,
        double radiusInKilometers,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(radiusInKilometers);
        var origin = await GeocodeAsync(postalCode, cancellationToken);
        if (origin is null)
            return new List<string>();

        var stores = await _storeService.GetNearbyStoresAsync(origin, radiusInKilometers, cancellationToken);
        return stores.Select(store => store.Name).ToList();
    }

    private static string CacheKey(string location) => $"opencage-geocode:{location}";

    private sealed class OpenCageResponse
    {
        [JsonPropertyName("results")]
        public List<OpenCageResult>? Results { get; set; }
    }

    private sealed class OpenCageResult
    {
        [JsonPropertyName("geometry")]
        public OpenCageGeometry? Geometry { get; set; }
    }

    private sealed class OpenCageGeometry
    {
        [JsonPropertyName("lat")]
        public double Latitude { get; set; }

        [JsonPropertyName("lng")]
        public double Longitude { get; set; }
    }
}
