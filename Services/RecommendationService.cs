using AIShoppingAssistant.Data;
using AIShoppingAssistant.DTOs;
using AIShoppingAssistant.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace AIShoppingAssistant.Services;

public sealed class RecommendationService
{
    private readonly IAIService _aiService;
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RecommendationService> _logger;
    private readonly FileUploadService _fileUploadService;

    public RecommendationService(
        IAIService aiService,
        ApplicationDbContext context,
        IMemoryCache cache,
        ILogger<RecommendationService> logger,
        FileUploadService fileUploadService)
    {
        _aiService = aiService;
        _context = context;
        _cache = cache;
        _logger = logger;
        _fileUploadService = fileUploadService;
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

    public async Task<InfiniteRecommendationResponseDto> GetInfiniteRecommendationsAsync(
        int userId, int page, int pageSize, string? category, decimal? minPrice, decimal? maxPrice,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"infinite-recommendations:user:{userId}";
        var ranked = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return await BuildInfiniteRecommendationsAsync(userId, cancellationToken);
        }) ?? [];

        var filtered = ranked.Where(item =>
            (string.IsNullOrWhiteSpace(category) || item.Product.Category.Equals(category.Trim(), StringComparison.OrdinalIgnoreCase)) &&
            (!minPrice.HasValue || item.Product.Price >= minPrice.Value) &&
            (!maxPrice.HasValue || item.Product.Price <= maxPrice.Value)).ToList();
        var products = filtered.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(item => new InfiniteRecommendedProductDto
            {
                Id = item.Product.Id, Name = item.Product.Name, Description = item.Product.Description,
                Price = item.Product.Price, Color = item.Product.Color, Size = item.Product.Size,
                StoreName = item.Product.StoreName, Category = item.Product.Category,
                ImageFileName = item.Product.ImageFileName,
                ImageUrl = _fileUploadService.GetImageUrl(item.Product.ImageFileName),
                Reason = item.Reason, ReasonType = item.ReasonType, AddedToShoppingList = item.AddedToShoppingList
            }).ToList();

        return new InfiniteRecommendationResponseDto
        {
            Products = products, TotalCount = filtered.Count, Page = page,
            HasMore = page * pageSize < filtered.Count
        };
    }

    private async Task<List<RankedRecommendation>> BuildInfiniteRecommendationsAsync(int userId, CancellationToken cancellationToken)
    {
        var products = await _context.Products.AsNoTracking().ToListAsync(cancellationToken);
        var preferences = await _context.UserPreferences.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken) ?? new UserPreference();
        var selectedItems = await _context.ShoppingListItems.AsNoTracking().Where(item => item.UserId == userId).ToListAsync(cancellationToken);
        var searches = await _context.SearchHistories.AsNoTracking().Where(item => item.UserId == userId).ToListAsync(cancellationToken);
        var purchases = await _context.PurchaseHistories.AsNoTracking().Where(item => item.UserId == userId).ToListAsync(cancellationToken);
        var allSelectedItems = await _context.ShoppingListItems.AsNoTracking().ToListAsync(cancellationToken);
        var allPurchases = await _context.PurchaseHistories.AsNoTracking().ToListAsync(cancellationToken);

        var purchasedIds = ExtractProductIds(purchases);
        var userProductIds = purchasedIds.Concat(selectedItems.Select(item => item.ProductId)).ToHashSet();
        var userProducts = products.Where(item => userProductIds.Contains(item.Id)).ToList();
        var storeCounts = userProducts.GroupBy(item => item.StoreName).ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var categoryCounts = userProducts.GroupBy(item => item.Category).ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var popularCounts = allSelectedItems.GroupBy(item => item.ProductId).ToDictionary(group => group.Key, group => group.Count());
        foreach (var productId in ExtractProductIds(allPurchases)) popularCounts[productId] = popularCounts.GetValueOrDefault(productId) + 1;
        var searchTerms = searches.Select(item => item.SearchTerm).Where(term => !string.IsNullOrWhiteSpace(term)).ToList();

        return products.Select(product =>
        {
            var score = 0;
            var reason = "Popular with shoppers";
            var reasonType = "popular";
            if (storeCounts.TryGetValue(product.StoreName, out var storeCount) && storeCount > 0)
            {
                score += 500 + storeCount * 10; reason = $"You often shop at {product.StoreName}"; reasonType = "purchase_history";
            }
            else if (categoryCounts.TryGetValue(product.Category, out var categoryCount) && categoryCount > 0)
            {
                score += 400 + categoryCount * 10; reason = $"Based on your {product.Category} purchases"; reasonType = "purchase_history";
            }
            else if (MatchesPreferences(product, preferences, searchTerms))
            {
                score += 300; reason = "Matches your saved preferences"; reasonType = "preferences";
            }
            else if (preferences.PreferredPriceRangeMax > 0 && product.Price >= preferences.PreferredPriceRangeMin && product.Price <= preferences.PreferredPriceRangeMax)
            {
                score += 200; reason = "Within your preferred price range"; reasonType = "price_range";
            }
            score += popularCounts.GetValueOrDefault(product.Id);
            return new RankedRecommendation(product, score, reason, reasonType, selectedItems.Any(item => item.ProductId == product.Id));
        }).OrderByDescending(item => item.Score).ThenBy(item => item.Product.Name).ToList();
    }

    private static bool MatchesPreferences(Product product, UserPreference preferences, IEnumerable<string> searches)
    {
        var text = $"{product.Name} {product.Description} {product.Category} {product.Color}";
        return preferences.FavoriteStores.Any(value => product.StoreName.Equals(value, StringComparison.OrdinalIgnoreCase)) ||
               preferences.FavoriteColors.Any(value => string.Equals(product.Color, value, StringComparison.OrdinalIgnoreCase)) ||
               preferences.FavoriteStyles.Concat(searches).Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<int> ExtractProductIds(IEnumerable<PurchaseHistory> purchases) => purchases.SelectMany(purchase =>
    {
        try { return JsonSerializer.Deserialize<List<PurchaseItemSnapshotDto>>(purchase.Items)?.Select(item => item.ProductId) ?? []; }
        catch (JsonException) { return []; }
    });

    private sealed record RankedRecommendation(Product Product, int Score, string Reason, string ReasonType, bool AddedToShoppingList);

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

    private RecommendedProductDto Map(Product product, ProductRecommendation recommendation) => new()
    {
        Id = product.Id,
        Name = product.Name,
        Price = product.Price,
        ShippingCost = product.ShippingCost,
        StoreName = product.StoreName,
        Category = product.Category,
        Color = product.Color,
        Size = product.Size,
        ImageUrl = _fileUploadService.GetImageUrl(product.ImageFileName),
        AiExplanation = recommendation.Reason,
        AiScore = recommendation.Score
    };
}
