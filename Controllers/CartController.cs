using System.Security.Claims;
using AIShoppingAssistant.Data;
using AIShoppingAssistant.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIShoppingAssistant.Controllers;

[Authorize]
public class CartController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;

    public CartController(ApplicationDbContext context, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("/Cart/Index")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("/Cart/Data")]
    public async Task<IActionResult> Data(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return Json(await BuildCartViewData(userId, cancellationToken));
    }

    [HttpPost("/Cart/Remove/{id:int}")]
    public async Task<IActionResult> Remove(int id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var cartItem = await _context.CartItems
            .SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken);

        if (cartItem is null)
        {
            return NotFound(new { message = "Cart item was not found." });
        }

        _context.CartItems.Remove(cartItem);
        await _context.SaveChangesAsync(cancellationToken);

        return Json(await BuildCartViewData(userId, cancellationToken));
    }

    [HttpPost("/Cart/Update/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCartQuantityDto request, CancellationToken cancellationToken)
    {
        if (request.Quantity is < 1 or > 999)
        {
            return BadRequest(new { message = "Quantity must be between 1 and 999." });
        }

        var userId = GetUserId();
        var cartItem = await _context.CartItems
            .SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken);

        if (cartItem is null)
        {
            return NotFound(new { message = "Cart item was not found." });
        }

        cartItem.Quantity = request.Quantity;
        await _context.SaveChangesAsync(cancellationToken);

        return Json(await BuildCartViewData(userId, cancellationToken));
    }

    [HttpPost("/Cart/Checkout")]
    public async Task<IActionResult> Checkout(CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        var apiUrl = $"{Request.Scheme}://{Request.Host}/api/shopping/cart/checkout";
        ForwardRequestCookies(client);

        var response = await client.PostAsync(apiUrl, null, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        return new ContentResult
        {
            Content = body,
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
            StatusCode = (int)response.StatusCode
        };
    }

    private async Task<object> BuildCartViewData(int userId, CancellationToken cancellationToken)
    {
        var cartItems = await _context.CartItems
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.AddedDate)
            .ToListAsync(cancellationToken);

        var productIds = cartItems.Select(item => item.ProductId).Distinct().ToList();
        var products = await _context.Products
            .AsNoTracking()
            .Where(product => productIds.Contains(product.Id))
            .Select(product => new { product.Id, product.ImageUrl, product.ShippingCost })
            .ToDictionaryAsync(product => product.Id, cancellationToken);

        var now = DateTime.UtcNow;
        var budget = await _context.Budgets
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId && item.Month == now.Month && item.Year == now.Year, cancellationToken);

        var rows = cartItems.Select(item =>
        {
            products.TryGetValue(item.ProductId, out var product);
            return new
            {
                id = item.Id,
                productId = item.ProductId,
                productName = item.ProductName,
                price = item.Price,
                quantity = item.Quantity,
                subtotal = item.Price * item.Quantity,
                imageUrl = product?.ImageUrl,
                shipping = product?.ShippingCost ?? 0m
            };
        }).ToList();

        var subtotal = rows.Sum(item => item.subtotal);
        var shipping = rows.Sum(item => item.shipping);
        var total = subtotal + shipping;
        var currentRemainingBudget = budget is null
            ? 0m
            : Math.Max(0m, budget.MonthlyAmount - budget.CurrentSpending);

        return new
        {
            items = rows,
            itemCount = rows.Sum(item => item.quantity),
            subtotal,
            shipping,
            total,
            remainingBudget = currentRemainingBudget,
            remainingBudgetAfterPurchase = currentRemainingBudget - total,
            exceedsBudget = budget is null || total > currentRemainingBudget,
            hasBudget = budget is not null
        };
    }

    private int GetUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("The current user session does not contain a valid name identifier claim.");
    }

    private void ForwardRequestCookies(HttpClient client)
    {
        if (Request.Headers.TryGetValue("Cookie", out var cookies))
        {
            client.DefaultRequestHeaders.Add("Cookie", cookies.ToString());
        }
    }

    public sealed class UpdateCartQuantityDto
    {
        public int Quantity { get; init; }
    }
}
