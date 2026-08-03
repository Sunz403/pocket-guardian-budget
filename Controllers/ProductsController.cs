using AIShoppingAssistant.Data;
using AIShoppingAssistant.DTOs;
using AIShoppingAssistant.Models;
using AIShoppingAssistant.Services;
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

    public ProductsController(ApplicationDbContext context, ILogger<ProductsController> logger, FileUploadService fileUploadService)
    {
        _context = context;
        _logger = logger;
        _fileUploadService = fileUploadService;
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
        [FromQuery] string? sortBy)
    {
        try
        {
            if (minPrice.HasValue && maxPrice.HasValue && minPrice > maxPrice)
            {
                return BadRequest(new { message = "minPrice cannot be greater than maxPrice." });
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

            productsQuery = sortBy switch
            {
                "priceLowToHigh" => productsQuery.OrderBy(product => product.Price),
                "priceHighToLow" => productsQuery.OrderByDescending(product => product.Price),
                "nameDesc" => productsQuery.OrderByDescending(product => product.Name),
                "nameAsc" => productsQuery.OrderBy(product => product.Name),
                null or "" => productsQuery.OrderBy(product => product.Name),
                _ => productsQuery.OrderBy(product => product.Name)
            };

            var products = (await productsQuery.ToListAsync())
                .Select(MapProductResponse)
                .ToList();

            return Ok(products);
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

    private ProductResponseDto MapProductResponse(Product product)
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
            Category = product.Category,
            ImageUrl = _fileUploadService.GetImageUrl(product.ImageFileName),
            CreatedAt = product.CreatedAt
        };
    }
}
