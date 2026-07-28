using System.Security.Claims;
using AIShoppingAssistant.Data;
using AIShoppingAssistant.DTOs;
using AIShoppingAssistant.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIShoppingAssistant.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class BudgetsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BudgetsController> _logger;

    public BudgetsController(ApplicationDbContext context, ILogger<BudgetsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("current")]
    public async Task<ActionResult<BudgetResponseDto>> GetCurrent()
    {
        try
        {
            if (!TryGetAuthenticatedUserId(out var userId))
            {
                return Unauthorized(new { message = "Invalid authentication session." });
            }

            var now = DateTime.UtcNow;
            var budget = await _context.Budgets
                .AsNoTracking()
                .SingleOrDefaultAsync(existingBudget =>
                    existingBudget.UserId == userId &&
                    existingBudget.Month == now.Month &&
                    existingBudget.Year == now.Year);

            if (budget is null)
            {
                return NotFound(new { message = "No budget found for the current month." });
            }

            return Ok(MapBudgetResponse(budget));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve the current budget.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Could not retrieve the current budget." });
        }
    }

    [HttpPost("set")]
    public async Task<ActionResult<BudgetResponseDto>> Set(BudgetSetDto budgetSetDto)
    {
        try
        {
            if (!TryGetAuthenticatedUserId(out var userId))
            {
                return Unauthorized(new { message = "Invalid authentication session." });
            }

            var now = DateTime.UtcNow;
            var month = budgetSetDto.Month ?? now.Month;
            var year = budgetSetDto.Year ?? now.Year;
            var budget = await _context.Budgets.SingleOrDefaultAsync(existingBudget =>
                existingBudget.UserId == userId &&
                existingBudget.Month == month &&
                existingBudget.Year == year);

            if (budget is null)
            {
                budget = new Budget
                {
                    UserId = userId,
                    Month = month,
                    Year = year
                };

                _context.Budgets.Add(budget);
            }

            budget.MonthlyAmount = budgetSetDto.MonthlyAmount;

            if (budgetSetDto.CurrentSpending.HasValue)
            {
                budget.CurrentSpending = budgetSetDto.CurrentSpending.Value;
            }

            await _context.SaveChangesAsync();

            return Ok(MapBudgetResponse(budget));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set the current month budget.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Could not set the budget." });
        }
    }

    [HttpPut("update-spending")]
    public async Task<ActionResult<BudgetResponseDto>> UpdateSpending([FromBody] decimal currentSpending)
    {
        try
        {
            if (currentSpending < 0)
            {
                return BadRequest(new { message = "Current spending cannot be negative." });
            }

            if (!TryGetAuthenticatedUserId(out var userId))
            {
                return Unauthorized(new { message = "Invalid authentication session." });
            }

            var now = DateTime.UtcNow;
            var budget = await _context.Budgets.SingleOrDefaultAsync(existingBudget =>
                existingBudget.UserId == userId &&
                existingBudget.Month == now.Month &&
                existingBudget.Year == now.Year);

            if (budget is null)
            {
                return NotFound(new { message = "No budget found for the current month." });
            }

            budget.CurrentSpending = currentSpending;
            await _context.SaveChangesAsync();

            return Ok(MapBudgetResponse(budget));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update current spending.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Could not update spending." });
        }
    }

    private bool TryGetAuthenticatedUserId(out int userId)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out userId);
    }

    private static BudgetResponseDto MapBudgetResponse(Budget budget)
    {
        return new BudgetResponseDto
        {
            Id = budget.Id,
            UserId = budget.UserId,
            MonthlyAmount = budget.MonthlyAmount,
            CurrentSpending = budget.CurrentSpending,
            RemainingAmount = budget.MonthlyAmount - budget.CurrentSpending,
            Month = budget.Month,
            Year = budget.Year
        };
    }
}
