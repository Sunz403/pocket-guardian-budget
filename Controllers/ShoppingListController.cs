using System.Security.Claims;
using System.Text.Json;
using AIShoppingAssistant.Data;
using AIShoppingAssistant.DTOs;
using AIShoppingAssistant.Models;
using AIShoppingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIShoppingAssistant.Controllers;

[Authorize]
public class ShoppingListController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ShoppingListController> _logger;
    private readonly IPersonalizedRecommendation _recommendations;

    public ShoppingListController(
        ApplicationDbContext context,
        ILogger<ShoppingListController> logger,
        IPersonalizedRecommendation recommendations)
    {
        _context = context;
        _logger = logger;
        _recommendations = recommendations;
    }

    [HttpGet("/ShoppingList")]
    public IActionResult Index() => View();

    [HttpGet("/ShoppingList/Data")]
    public async Task<IActionResult> Data(CancellationToken cancellationToken) => Json(await BuildData(GetUserId(), cancellationToken));

    [HttpPost("/ShoppingList/Add/{productId:int}")]
    public async Task<IActionResult> Add(int productId, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserId();
            var product = await _context.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == productId, cancellationToken);
            if (product is null) return NotFound(new { message = "Product not found." });

            var alreadySelected = await _context.ShoppingListItems.AnyAsync(i => i.UserId == userId && i.ProductId == productId, cancellationToken);
            if (!alreadySelected)
            {
                _context.ShoppingListItems.Add(new ShoppingListItem { UserId = userId, ProductId = product.Id, ProductName = product.Name, Price = product.Price });
                await _context.SaveChangesAsync(cancellationToken);
            }

            return Json(await BuildData(userId, cancellationToken));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not add product {ProductId} to the shopping list.", productId);
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Json(new { message = "We couldn't update your Shopping List. Please restart the app so its database updates can be applied, then try again." });
        }
    }

    [HttpPost("/ShoppingList/Remove/{id:int}")]
    public async Task<IActionResult> Remove(int id, CancellationToken cancellationToken)
    {
        var item = await _context.ShoppingListItems.SingleOrDefaultAsync(i => i.Id == id && i.UserId == GetUserId(), cancellationToken);
        if (item is null) return NotFound(new { message = "Selected item not found." });
        _context.ShoppingListItems.Remove(item);
        await _context.SaveChangesAsync(cancellationToken);
        return Json(await BuildData(GetUserId(), cancellationToken));
    }

    [HttpPost("/ShoppingList/Clear")]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var items = await _context.ShoppingListItems.Where(i => i.UserId == userId).ToListAsync(cancellationToken);
        _context.ShoppingListItems.RemoveRange(items);
        await _context.SaveChangesAsync(cancellationToken);
        return Json(await BuildData(userId, cancellationToken));
    }

    [HttpPost("/ShoppingList/Purchase/{id:int}")]
    public async Task<IActionResult> Purchase(int id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var item = await _context.ShoppingListItems
            .SingleOrDefaultAsync(existing => existing.Id == id && existing.UserId == userId, cancellationToken);
        if (item is null) return NotFound(new { message = "Selected item not found." });

        var product = await _context.Products.AsNoTracking()
            .SingleOrDefaultAsync(existing => existing.Id == item.ProductId, cancellationToken);
        if (product is null) return NotFound(new { message = "Product not found." });

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);
        var budget = await _context.Budgets.SingleOrDefaultAsync(existing =>
            existing.UserId == userId && existing.Month == now.Month && existing.Year == now.Year,
            cancellationToken);
        var spendingBeforeThisPurchase = await _context.PurchaseHistories
            .AsNoTracking()
            .Where(purchase => purchase.UserId == userId && purchase.PurchaseDate >= monthStart && purchase.PurchaseDate < monthEnd)
            .SumAsync(purchase => (decimal?)purchase.TotalAmount, cancellationToken) ?? 0m;
        var remainingBudget = Math.Max(0m, (budget?.MonthlyAmount ?? 0m) - spendingBeforeThisPurchase);

        if (budget is null || product.Price > remainingBudget)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new
            {
                insufficientBalance = true,
                remainingBudget,
                message = budget is null
                    ? "Set a monthly budget before recording a purchase."
                    : $"You only have R{remainingBudget:0.00} remaining, which is not enough to purchase {product.Name}."
            });
        }

        _context.PurchaseHistories.Add(new PurchaseHistory
        {
            UserId = userId,
            PurchaseDate = now,
            TotalAmount = product.Price,
            Items = JsonSerializer.Serialize(new List<PurchaseItemSnapshotDto>
            {
                new()
                {
                    ProductId = product.Id, ProductName = product.Name, Price = product.Price,
                    Quantity = 1, LineTotal = product.Price, AddedDate = now,
                    StoreName = product.StoreName, Category = product.Category, Color = product.Color
                }
            })
        });

        var preferences = await _context.UserPreferences
            .SingleOrDefaultAsync(existing => existing.UserId == userId, cancellationToken);
        if (preferences is null)
        {
            preferences = new UserPreference { UserId = userId };
            _context.UserPreferences.Add(preferences);
        }
        AddIfMissing(preferences.FavoriteStores, product.StoreName);
        AddIfMissing(preferences.FavoriteColors, product.Color);
        AddIfMissing(preferences.FavoriteStyles, product.Category);

        budget.CurrentSpending = spendingBeforeThisPurchase + product.Price;

        _context.ShoppingListItems.Remove(item);
        await _context.SaveChangesAsync(cancellationToken);
        _recommendations.InvalidateUserCache(userId);

        var data = await BuildData(userId, cancellationToken);
        return Json(new
        {
            message = "Purchase recorded and your recommendations have been updated.",
            data
        });
    }

    private async Task<object> BuildData(int userId, CancellationToken cancellationToken)
    {
        var items = await _context.ShoppingListItems.AsNoTracking().Where(i => i.UserId == userId).OrderBy(i => i.SelectedDate).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var budget = await _context.Budgets.AsNoTracking().SingleOrDefaultAsync(b => b.UserId == userId && b.Month == now.Month && b.Year == now.Year, cancellationToken);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);
        var currentSpending = await _context.PurchaseHistories
            .AsNoTracking()
            .Where(purchase => purchase.UserId == userId && purchase.PurchaseDate >= monthStart && purchase.PurchaseDate < monthEnd)
            .SumAsync(purchase => (decimal?)purchase.TotalAmount, cancellationToken) ?? 0m;
        var monthlyBudget = budget?.MonthlyAmount ?? 0m;
        var selectedTotal = items.Sum(i => i.Price);
        var remainingBudget = Math.Max(0m, monthlyBudget - currentSpending);
        var percentageUsed = monthlyBudget == 0 ? 0m : Math.Round(currentSpending / monthlyBudget * 100m, 2);
        var budgetLimitReached = monthlyBudget > 0m && currentSpending >= monthlyBudget;

        return new
        {
            items = items.Select(i => new { i.Id, i.ProductId, i.ProductName, i.Price }),
            monthlyBudget,
            currentSpending,
            selectedTotal,
            remainingBudget,
            percentageUsed,
            budgetLimitReached
        };
    }

    private int GetUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : throw new UnauthorizedAccessException();

    private static void AddIfMissing(List<string> values, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !values.Any(existing =>
                existing.Equals(value, StringComparison.OrdinalIgnoreCase)))
            values.Add(value.Trim());
    }
}
