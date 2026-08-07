using System.Security.Claims;
using System.Text.Json;
using AIShoppingAssistant.Data;
using AIShoppingAssistant.DTOs;
using AIShoppingAssistant.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIShoppingAssistant.Controllers;

[Authorize]
public class BudgetController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BudgetController> _logger;

    public BudgetController(ApplicationDbContext context, ILogger<BudgetController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("/Budget/Index")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("/Budget/GetBudgetSummary")]
    public async Task<IActionResult> GetBudgetSummary(int? month, int? year, CancellationToken cancellationToken)
    {
        try
        {
            var selectedDate = GetSelectedDate(month, year);
            var userId = GetUserId();
            var summary = await BuildBudgetSummary(userId, selectedDate.Month, selectedDate.Year, cancellationToken);

            return Json(new
            {
                success = true,
                monthlyAmount = summary.MonthlyAmount,
                currentSpending = summary.CurrentSpending,
                remaining = summary.Remaining,
                percentageUsed = summary.PercentageUsed,
                month = selectedDate.Month,
                year = selectedDate.Year
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load budget summary.");
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Json(new { success = false, message = "Could not load your budget summary." });
        }
    }

    [HttpGet("/Budget/GetSpendingHistory")]
    public async Task<IActionResult> GetSpendingHistory(int? month, int? year, CancellationToken cancellationToken)
    {
        try
        {
            var selectedDate = GetSelectedDate(month, year);
            var purchases = await GetMonthlyPurchases(GetUserId(), selectedDate.Month, selectedDate.Year, cancellationToken);
            var rows = await BuildSpendingRows(purchases, cancellationToken);

            return Json(new
            {
                success = true,
                month = selectedDate.Month,
                year = selectedDate.Year,
                totalItemsPurchased = rows.Sum(row => row.Quantity),
                purchases = rows
                    .OrderByDescending(row => row.Date)
                    .Take(10)
                    .Select(row => new
                    {
                        date = row.Date,
                        product = row.Product,
                        amount = row.Amount,
                        store = row.Store,
                        category = row.Category,
                        quantity = row.Quantity
                    }),
                categories = rows
                    .GroupBy(row => row.Category)
                    .Select(group => new
                    {
                        category = group.Key,
                        amount = group.Sum(row => row.Amount)
                    })
                    .OrderByDescending(item => item.amount)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load spending history.");
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Json(new { success = false, message = "Could not load your spending history." });
        }
    }

    [HttpPost("/Budget/SetBudget")]
    public async Task<IActionResult> SetBudget([FromBody] BudgetSetDto request, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return Json(new { success = false, message = "Enter a valid monthly budget amount." });
            }

            var selectedDate = GetSelectedDate(request.Month, request.Year);
            var userId = GetUserId();
            var budget = await _context.Budgets.SingleOrDefaultAsync(existingBudget =>
                existingBudget.UserId == userId &&
                existingBudget.Month == selectedDate.Month &&
                existingBudget.Year == selectedDate.Year,
                cancellationToken);

            if (budget is null)
            {
                budget = new Budget
                {
                    UserId = userId,
                    Month = selectedDate.Month,
                    Year = selectedDate.Year
                };

                _context.Budgets.Add(budget);
            }

            budget.MonthlyAmount = request.MonthlyAmount;

            if (request.CurrentSpending.HasValue)
            {
                budget.CurrentSpending = request.CurrentSpending.Value;
            }

            await _context.SaveChangesAsync(cancellationToken);
            var summary = await BuildBudgetSummary(userId, selectedDate.Month, selectedDate.Year, cancellationToken);

            return Json(new
            {
                success = true,
                message = "Budget saved successfully.",
                monthlyAmount = summary.MonthlyAmount,
                currentSpending = summary.CurrentSpending,
                remaining = summary.Remaining,
                percentageUsed = summary.PercentageUsed
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save budget.");
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Json(new { success = false, message = "Could not save your budget." });
        }
    }

    private async Task<BudgetSummary> BuildBudgetSummary(
        int userId,
        int month,
        int year,
        CancellationToken cancellationToken)
    {
        var budget = await _context.Budgets
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.UserId == userId &&
                item.Month == month &&
                item.Year == year,
                cancellationToken);

        var purchases = await GetMonthlyPurchases(userId, month, year, cancellationToken);
        var rows = await BuildSpendingRows(purchases, cancellationToken);
        // Purchase history is the shared source of truth for every budget view.
        var currentSpending = purchases.Sum(purchase => purchase.TotalAmount);
        var monthlyAmount = budget?.MonthlyAmount ?? 0m;
        var remaining = Math.Max(0m, monthlyAmount - currentSpending);
        var percentageUsed = monthlyAmount <= 0m ? 0m : Math.Round(currentSpending / monthlyAmount * 100m, 2);

        return new BudgetSummary(monthlyAmount, currentSpending, remaining, percentageUsed);
    }

    private async Task<List<PurchaseSnapshot>> GetMonthlyPurchases(
        int userId,
        int month,
        int year,
        CancellationToken cancellationToken)
    {
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);

        return await _context.PurchaseHistories
            .AsNoTracking()
            .Where(purchase =>
                purchase.UserId == userId &&
                purchase.PurchaseDate >= start &&
                purchase.PurchaseDate < end)
            .Select(purchase => new PurchaseSnapshot(purchase.PurchaseDate, purchase.Items, purchase.TotalAmount))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<SpendingRow>> BuildSpendingRows(
        IEnumerable<PurchaseSnapshot> purchases,
        CancellationToken cancellationToken)
    {
        var rows = new List<SpendingRow>();
        var productIds = new HashSet<int>();

        foreach (var purchase in purchases)
        {
            foreach (var item in DeserializeItems(purchase.Items))
            {
                productIds.Add(item.ProductId);
                rows.Add(new SpendingRow(
                    purchase.PurchaseDate,
                    item.ProductName,
                    item.LineTotal,
                    item.Quantity,
                    item.ProductId,
                    "Unknown",
                    "Uncategorized"));
            }
        }

        if (productIds.Count == 0)
        {
            return rows;
        }

        var products = await _context.Products
            .AsNoTracking()
            .Where(product => productIds.Contains(product.Id))
            .Select(product => new { product.Id, product.StoreName, product.Category })
            .ToDictionaryAsync(product => product.Id, cancellationToken);

        return rows.Select(row =>
        {
            if (!products.TryGetValue(row.ProductId, out var product))
            {
                return row;
            }

            return row with
            {
                Store = string.IsNullOrWhiteSpace(product.StoreName) ? "Unknown" : product.StoreName,
                Category = string.IsNullOrWhiteSpace(product.Category) ? "Uncategorized" : product.Category
            };
        }).ToList();
    }

    private static IReadOnlyList<PurchaseItemSnapshotDto> DeserializeItems(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<PurchaseItemSnapshotDto>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static DateTime GetSelectedDate(int? month, int? year)
    {
        var now = DateTime.UtcNow;
        return new DateTime(year ?? now.Year, month ?? now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private int GetUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("The current user session does not contain a valid name identifier claim.");
    }

    private sealed record BudgetSummary(
        decimal MonthlyAmount,
        decimal CurrentSpending,
        decimal Remaining,
        decimal PercentageUsed);

    private sealed record PurchaseSnapshot(DateTime PurchaseDate, string Items, decimal TotalAmount);

    private sealed record SpendingRow(
        DateTime Date,
        string Product,
        decimal Amount,
        int Quantity,
        int ProductId,
        string Store,
        string Category);
}
