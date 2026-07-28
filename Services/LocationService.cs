using System.Text.Json;
using System.Text.Json.Serialization;
using AIShoppingAssistant.DTOs;
using Microsoft.Extensions.Options;

namespace AIShoppingAssistant.Services;

public sealed class LocationService
{
    private const double EarthRadiusKm = 6371.0088;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OpenCageOptions _openCageOptions;
    private readonly StoreLocationsOptions _storeLocations;
    private readonly ILogger<LocationService> _logger;

    public LocationService(
        IHttpClientFactory httpClientFactory,
        IOptions<OpenCageOptions> openCageOptions,
        IOptions<StoreLocationsOptions> storeLocations,
        ILogger<LocationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _openCageOptions = openCageOptions.Value;
        _storeLocations = storeLocations.Value;
        _logger = logger;
    }

    public async Task<GeoCoordinates?> ValidatePostalCodeAsync(
        string postalCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(postalCode);
        if (string.IsNullOrWhiteSpace(_openCageOptions.ApiKey))
        {
            _logger.LogError("OpenCage API key is not configured.");
            return null;
        }

        var client = _httpClientFactory.CreateClient("OpenCage");
        var requestUri = $"geocode/v1/json?q={Uri.EscapeDataString(postalCode)}&key={Uri.EscapeDataString(_openCageOptions.ApiKey)}&limit=1&no_annotations=1";
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
            return geometry is null ? null : new GeoCoordinates
            {
                Latitude = geometry.Latitude,
                Longitude = geometry.Longitude
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "OpenCage could not validate postal code {PostalCode}.", postalCode);
            return null;
        }
    }

    public async Task<double?> CalculateDistanceAsync(
        string firstPostalCode,
        string secondPostalCode,
        CancellationToken cancellationToken = default)
    {
        var first = await ValidatePostalCodeAsync(firstPostalCode, cancellationToken);
        var second = await ValidatePostalCodeAsync(secondPostalCode, cancellationToken);
        return first is null || second is null ? null : CalculateDistanceInKilometers(first, second);
    }

    public async Task<List<string>> GetNearbyStoresAsync(
        string postalCode,
        double radiusInKilometers,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(radiusInKilometers);
        var origin = await ValidatePostalCodeAsync(postalCode, cancellationToken);
        if (origin is null)
            return new List<string>();

        return _storeLocations.Stores
            .Where(store => !string.IsNullOrWhiteSpace(store.Name))
            .Where(store => CalculateDistanceInKilometers(origin, new GeoCoordinates
            {
                Latitude = store.Latitude,
                Longitude = store.Longitude
            }) <= radiusInKilometers)
            .Select(store => store.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static double CalculateDistanceInKilometers(GeoCoordinates first, GeoCoordinates second)
    {
        var latitudeDelta = ToRadians(second.Latitude - first.Latitude);
        var longitudeDelta = ToRadians(second.Longitude - first.Longitude);
        var a = Math.Pow(Math.Sin(latitudeDelta / 2), 2)
            + Math.Cos(ToRadians(first.Latitude)) * Math.Cos(ToRadians(second.Latitude))
            * Math.Pow(Math.Sin(longitudeDelta / 2), 2);
        return EarthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;

    private sealed class OpenCageResponse
    {
        public List<OpenCageResult>? Results { get; set; }
    }

    private sealed class OpenCageResult
    {
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
