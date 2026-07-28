using AIShoppingAssistant.Data;
using AIShoppingAssistant.DTOs;
using AIShoppingAssistant.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AIShoppingAssistant.Services;

public sealed class RecommendationService
{
    private readonly IAIService _aiService;
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RecommendationService> _logger;

    public RecommendationService(
        IAIService aiService,
        ApplicationDbContext context,
        IMemoryCache cache,
        ILogger<RecommendationService> logger)
    {
        _aiService = aiService;
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<RecommendedProductDto>> GetPersonalizedRecommendationsAsync(
        int userId,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        limit = Math.Clamp(limit, 1, 10);
        var cacheKey = $"recommendations:user:{userId}:limit:{limit}";

        try
        {
            var result = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);

            // The recent searches are intentionally loaded for the user's recommendation context.
            var recentSearches = await _context.SearchHistories
                .AsNoTracking()
                .Where(search => search.UserId == userId)
                .OrderByDescending(search => search.SearchDate)
                .Take(10)
                .ToListAsync(cancellationToken);
            var preferences = await _context.UserPreferences
                .AsNoTracking()
                .SingleOrDefaultAsync(preference => preference.UserId == userId, cancellationToken)
                ?? BuildPreferencesFromSearches(userId, recentSearches);
            var products = await _context.Products.AsNoTracking().ToListAsync(cancellationToken);
            var currentBudget = await GetCurrentBudgetAsync(userId, cancellationToken);
            var availableBudget = currentBudget is null
                ? recentSearches.FirstOrDefault()?.Budget ?? 0m
                : Math.Max(0m, currentBudget.MonthlyAmount - currentBudget.CurrentSpending);

                var aiResult = await _aiService.GetRecommendationAsync(
                    products, availableBudget, preferences, cancellationToken);
                var productsById = products.ToDictionary(product => product.Id);

                return aiResult.Recommendations
                    .Where(recommendation => productsById.ContainsKey(recommendation.ProductId))
                    .Take(limit)
                    .Select(recommendation => Map(productsById[recommendation.ProductId], recommendation))
                    .ToList();
            });

            return result ?? new List<RecommendedProductDto>();
        }
        catch (LocalAIUnavailableException ex)
        {
            // Do not cache a service outage; the next request can retry once Ollama is running.
            _logger.LogError(ex, "Local Ollama model was unavailable while recommending products for user {UserId}.", userId);
            return new List<RecommendedProductDto>();
        }
    }

    public async Task<BudgetSummaryDto> GetBudgetSummaryAsync(int userId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        var budget = await GetCurrentBudgetAsync(userId, cancellationToken);
        if (budget is null)
            return new BudgetSummaryDto();

        var remaining = budget.MonthlyAmount - budget.CurrentSpending;
        return new BudgetSummaryDto
        {
            BudgetAmount = budget.MonthlyAmount,
            CurrentSpending = budget.CurrentSpending,
            RemainingAmount = remaining,
            PercentageUsed = budget.MonthlyAmount == 0m
                ? 0m
                : Math.Round((budget.CurrentSpending / budget.MonthlyAmount) * 100m, 2)
        };
    }

    private async Task<Budget?> GetCurrentBudgetAsync(int userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return await _context.Budgets.AsNoTracking().SingleOrDefaultAsync(budget =>
            budget.UserId == userId && budget.Month == now.Month && budget.Year == now.Year,
            cancellationToken);
    }

    private static UserPreference BuildPreferencesFromSearches(int userId, IEnumerable<SearchHistory> searches) => new()
    {
        UserId = userId,
        FavoriteStyles = searches.Select(search => search.SearchTerm).Take(4).ToList()
    };

    private static RecommendedProductDto Map(Product product, ProductRecommendation recommendation) => new()
    {
        Id = product.Id,
        Name = product.Name,
        Price = product.Price,
        ShippingCost = product.ShippingCost,
        StoreName = product.StoreName,
        Category = product.Category,
        Color = product.Color,
        Size = product.Size,
        ImageUrl = product.ImageUrl,
        AiExplanation = recommendation.Reason,
        AiScore = recommendation.Score
    };
}
