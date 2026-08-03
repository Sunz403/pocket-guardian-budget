using System.Security.Claims;
using AIShoppingAssistant.Data;
using AIShoppingAssistant.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIShoppingAssistant.Controllers;

[Authorize]
public class ShoppingListController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ShoppingListController> _logger;

    public ShoppingListController(ApplicationDbContext context, ILogger<ShoppingListController> logger)
    {
        _context = context;
        _logger = logger;
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

    private async Task<object> BuildData(int userId, CancellationToken cancellationToken)
    {
        var items = await _context.ShoppingListItems.AsNoTracking().Where(i => i.UserId == userId).OrderBy(i => i.SelectedDate).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var budget = await _context.Budgets.AsNoTracking().SingleOrDefaultAsync(b => b.UserId == userId && b.Month == now.Month && b.Year == now.Year, cancellationToken);
        var monthlyBudget = budget?.MonthlyAmount ?? 5000m;
        var selectedTotal = items.Sum(i => i.Price);
        return new { items = items.Select(i => new { i.Id, i.ProductId, i.ProductName, i.Price }), monthlyBudget, selectedTotal, remainingBudget = monthlyBudget - selectedTotal, percentageUsed = monthlyBudget == 0 ? 0 : Math.Round(selectedTotal / monthlyBudget * 100m, 2) };
    }

    private int GetUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : throw new UnauthorizedAccessException();
}
