using AIShoppingAssistant.Data;
using AIShoppingAssistant.DTOs;
using AIShoppingAssistant.Models;
using AIShoppingAssistant.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIShoppingAssistant.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProductsController> _logger;
    private readonly FileUploadService _fileUploadService;
    private readonly LocationService _locationService;

    public ProductsController(ApplicationDbContext context, ILogger<ProductsController> logger, FileUploadService fileUploadService, LocationService locationService)
    {
        _context = context;
        _logger = logger;
        _fileUploadService = fileUploadService;
        _locationService = locationService;
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<ProductResponseDto>>> Search(
        [FromQuery] string? keyword,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] string? color,
        [FromQuery] string? size,
        [FromQuery] string? category,
        [FromQuery] string? storeName,
        [FromQuery] string? sortBy,
        [FromQuery] string? location,
        [FromQuery] double? radiusKm,
        CancellationToken cancellationToken)
    {
        try
        {
            if (minPrice.HasValue && maxPrice.HasValue && minPrice > maxPrice)
            {
                return BadRequest(new { message = "minPrice cannot be greater than maxPrice." });
            }

            if (radiusKm.HasValue && radiusKm is not (5 or 10 or 25 or 50))
            {
                return BadRequest(new { message = "radiusKm must be 5, 10, 25, or 50." });
            }

            var productsQuery = _context.Products.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var keywordValue = keyword.Trim();
                productsQuery = productsQuery.Where(product =>
                    product.Name.Contains(keywordValue) ||
                    (product.Description != null && product.Description.Contains(keywordValue)) ||
                    product.Category.Contains(keywordValue));
            }

            if (minPrice.HasValue)
            {
                productsQuery = productsQuery.Where(product => product.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                productsQuery = productsQuery.Where(product => product.Price <= maxPrice.Value);
            }

            if (!string.IsNullOrWhiteSpace(color))
            {
                var colorValue = color.Trim();
                productsQuery = productsQuery.Where(product => product.Color != null && product.Color == colorValue);
            }

            if (!string.IsNullOrWhiteSpace(size))
            {
                var sizeValue = size.Trim();
                productsQuery = productsQuery.Where(product => product.Size != null && product.Size == sizeValue);
            }

            if (!string.IsNullOrWhiteSpace(storeName))
            {
                var storeNameValue = storeName.Trim();
                productsQuery = productsQuery.Where(product => product.StoreName == storeNameValue);
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                var categoryValue = category.Trim();
                productsQuery = productsQuery.Where(product => product.Category == categoryValue);
            }

            // Distance requires Haversine calculation and therefore happens after the
            // stores have been loaded. Database sorting is used for normal searches.
            if (string.IsNullOrWhiteSpace(location) || sortBy is not ("distance" or "distanceAsc"))
            {
                productsQuery = sortBy switch
                {
                    "priceLowToHigh" => productsQuery.OrderBy(product => product.Price),
                    "priceHighToLow" => productsQuery.OrderByDescending(product => product.Price),
                    "nameDesc" => productsQuery.OrderByDescending(product => product.Name),
                    "nameAsc" => productsQuery.OrderBy(product => product.Name),
                    _ => productsQuery.OrderBy(product => product.Name)
                };
            }

            var productEntities = await productsQuery.Include(product => product.Store).ToListAsync(cancellationToken);
            GeoCoordinates? origin = null;
            if (!string.IsNullOrWhiteSpace(location))
            {
                origin = await _locationService.ValidatePostalCodeAsync(location, cancellationToken);
                if (origin is null)
                {
                    return BadRequest(new { message = "We could not find that postal code. Please enter a valid South African postal code." });
                }
            }

            var effectiveRadius = radiusKm ?? 25;
            IEnumerable<ProductResponseDto> products = productEntities
                .Select(product => MapProductResponse(product, origin))
                .Where(product => origin is null || (product.DistanceKm.HasValue && product.DistanceKm <= effectiveRadius));

            if (origin is not null && sortBy is "distance" or "distanceAsc")
            {
                products = products.OrderBy(product => product.DistanceKm);
            }

            return Ok(products.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search products.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Could not search products." });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductResponseDto>> GetById(int id)
    {
        try
        {
            var product = await _context.Products
                .AsNoTracking()
                .Where(existingProduct => existingProduct.Id == id)
                .SingleOrDefaultAsync();

            if (product is null)
            {
                return NotFound(new { message = "Product not found." });
            }

            return Ok(MapProductResponse(product));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve product with ID {ProductId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Could not retrieve product." });
        }
    }

    [Authorize]
    [HttpPost("{id:int}/visit")]
    public async Task<IActionResult> VisitStore(int id, CancellationToken cancellationToken)
    {
        var product = await _context.Products.AsNoTracking()
            .SingleOrDefaultAsync(existingProduct => existingProduct.Id == id, cancellationToken);
        if (product is null) return NotFound(new { message = "Product not found." });

        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        var destination = GetDestination(product);
        _context.SearchHistories.Add(new SearchHistory
        {
            UserId = userId,
            SearchTerm = $"Store visit: {product.Name}",
            Budget = 0m,
            Location = product.StoreName,
            SearchDate = DateTime.UtcNow,
            ResultsCount = 1
        });
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { url = destination, storeName = product.StoreName, hasProductUrl = !string.IsNullOrWhiteSpace(product.StoreUrl) });
    }

    private ProductResponseDto MapProductResponse(Product product, GeoCoordinates? origin = null)
    {
        return new ProductResponseDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            Color = product.Color,
            Size = product.Size,
            ShippingCost = product.ShippingCost,
            StoreName = product.StoreName,
            StoreUrl = product.StoreUrl,
            StoreAddress = product.Store?.Address,
            DistanceKm = origin is not null && product.Store is not null
                ? Math.Round(StoreService.CalculateDistanceInKilometers(origin, new GeoCoordinates
                {
                    Latitude = product.Store.Latitude,
                    Longitude = product.Store.Longitude
                }), 1)
                : null,
            Category = product.Category,
            ImageUrl = _fileUploadService.GetImageUrl(product.ImageFileName),
            CreatedAt = product.CreatedAt
        };
    }

    private static string? GetDestination(Product product)
    {
        if (Uri.TryCreate(product.StoreUrl, UriKind.Absolute, out var storeUri) &&
            (storeUri.Scheme == Uri.UriSchemeHttp || storeUri.Scheme == Uri.UriSchemeHttps))
        {
            return storeUri.AbsoluteUri;
        }

        return product.StoreName.Trim().ToLowerInvariant() switch
        {
            "woolworths" => "https://www.woolworths.co.za/",
            "takealot" => "https://www.takealot.com/",
            "checkers" => "https://shop.checkers.co.za/",
            "game" => "https://www.game.co.za/",
            _ => null
        };
    }
}
