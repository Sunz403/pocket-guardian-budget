using System.Text.Json;
using AIShoppingAssistant.Data;
using AIShoppingAssistant.DTOs;
using AIShoppingAssistant.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AIShoppingAssistant.Services;

/// <summary>
/// A deterministic recommendation engine.  The score bands deliberately do not overlap,
/// so a store match always outranks a category match, and so on.
/// </summary>
public sealed class RecommendationService : IPersonalizedRecommendation
{
    private const string CachePrefix = "personalized-recommendations:user:";
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly FileUploadService _fileUploadService;

    public RecommendationService(
        ApplicationDbContext context,
        IMemoryCache cache,
        FileUploadService fileUploadService)
    {
        _context = context;
        _cache = cache;
        _fileUploadService = fileUploadService;
    }

    public async Task<IReadOnlyList<PersonalizedRecommendationDto>> GetPersonalizedRecommendationsAsync(
        int userId,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        limit = Math.Clamp(limit, 1, 10);
        var cacheKey = $"{CachePrefix}{userId}:limit:{limit}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            return await BuildRecommendationsAsync(userId, limit, cancellationToken);
        }) ?? [];
    }

    // Kept as the concise API requested by callers; the Async suffix is available for .NET convention.
    public Task<IReadOnlyList<PersonalizedRecommendationDto>> GetPersonalizedRecommendations(
        int userId,
        int limit = 10,
        CancellationToken cancellationToken = default) =>
        GetPersonalizedRecommendationsAsync(userId, limit, cancellationToken);

    public void InvalidateUserCache(int userId)
    {
        for (var limit = 1; limit <= 10; limit++)
            _cache.Remove($"{CachePrefix}{userId}:limit:{limit}");

        _cache.Remove($"infinite-recommendations:user:{userId}");
    }

    // Kept for the existing infinite-scroll API. It uses the same scoring and reasons.
    public async Task<InfiniteRecommendationResponseDto> GetInfiniteRecommendationsAsync(
        int userId, int page, int pageSize, string? category, decimal? minPrice, decimal? maxPrice,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"infinite-recommendations:user:{userId}";
        var ranked = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            return await BuildRecommendationsAsync(userId, int.MaxValue, cancellationToken);
        }) ?? [];

        var filtered = ranked.Where(item =>
            (string.IsNullOrWhiteSpace(category) || item.Category.Equals(category.Trim(), StringComparison.OrdinalIgnoreCase)) &&
            (!minPrice.HasValue || item.Price >= minPrice.Value) &&
            (!maxPrice.HasValue || item.Price <= maxPrice.Value)).ToList();

        return new InfiniteRecommendationResponseDto
        {
            Products = filtered.Skip((page - 1) * pageSize).Take(pageSize).Select(item => new InfiniteRecommendedProductDto
            {
                Id = item.Id, Name = item.Name, Description = item.Description, Price = item.Price,
                Color = item.Color, Size = item.Size, StoreName = item.StoreName, Category = item.Category,
                ImageUrl = item.ImageUrl, Reason = item.Reason, ReasonType = item.ReasonType
            }).ToList(),
            TotalCount = filtered.Count,
            Page = page,
            HasMore = page * pageSize < filtered.Count
        };
    }

    private async Task<IReadOnlyList<PersonalizedRecommendationDto>> BuildRecommendationsAsync(
        int userId, int limit, CancellationToken cancellationToken)
    {
        // Run database operations sequentially: a DbContext does not support concurrent queries.
        var products = await _context.Products.AsNoTracking().ToListAsync(cancellationToken);
        var preferences = await _context.UserPreferences.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken) ?? new UserPreference { UserId = userId };
        var purchases = await _context.PurchaseHistories.AsNoTracking()
            .Where(item => item.UserId == userId).ToListAsync(cancellationToken);
        var searches = await _context.SearchHistories.AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.SearchDate).Take(10).ToListAsync(cancellationToken);
        var allPurchases = await _context.PurchaseHistories.AsNoTracking().ToListAsync(cancellationToken);
        var allListItems = await _context.ShoppingListItems.AsNoTracking().ToListAsync(cancellationToken);
        var allSearches = await _context.SearchHistories.AsNoTracking().ToListAsync(cancellationToken);

        var productById = products.ToDictionary(item => item.Id);
        var ownPurchaseSignals = ReadPurchaseSignals(purchases, productById);
        var allPurchaseSignals = ReadPurchaseSignals(allPurchases, productById);
        var storeCounts = Count(ownPurchaseSignals.Select(item => item.StoreName));
        var categoryCounts = Count(ownPurchaseSignals.Select(item => item.Category));
        var popularProductCounts = allPurchaseSignals.Where(item => item.ProductId > 0)
            .GroupBy(item => item.ProductId).ToDictionary(group => group.Key, group => group.Count());
        foreach (var item in allListItems)
            popularProductCounts[item.ProductId] = popularProductCounts.GetValueOrDefault(item.ProductId) + 1;

        var recentSearches = searches.Select(item => item.SearchTerm).Where(IsUsefulSearch).ToList();
        var priceRange = await GetPreferredPriceRange(preferences, userId, cancellationToken);
        var globalSearchTerms = allSearches.Select(item => item.SearchTerm).Where(IsUsefulSearch).ToList();

        var ranked = new List<PersonalizedRecommendationDto>(products.Count);
        foreach (var product in products)
        {
            var match = GetHighestPriorityMatch(product, preferences, storeCounts, categoryCounts, recentSearches, priceRange);
            var popularity = popularProductCounts.GetValueOrDefault(product.Id) +
                             globalSearchTerms.Count(term => SearchMatches(product, term));

            // Popularity only breaks ties within a higher-priority band. It is the fifth fallback.
            var score = match.Score + Math.Min(popularity, 99);
            if (match.Score == 0 && popularity > 0)
                match = new RecommendationMatch(1, "Popular with other students", "popular");

            ranked.Add(new PersonalizedRecommendationDto
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                Color = product.Color,
                Size = product.Size,
                StoreName = product.StoreName,
                Category = product.Category,
                ImageUrl = _fileUploadService.GetImageUrl(product.ImageFileName),
                Reason = match.Reason,
                ReasonType = match.ReasonType,
                Score = score
            });
        }

        return ranked.OrderByDescending(item => item.Score)
            .ThenBy(item => item.Name)
            .Take(limit)
            .ToList();
    }

    private async Task<(decimal Min, decimal Max)> GetPreferredPriceRange(
        UserPreference preferences, int userId, CancellationToken cancellationToken)
    {
        if (preferences.PreferredPriceRangeMax > 0)
            return (preferences.PreferredPriceRangeMin, preferences.PreferredPriceRangeMax);

        var now = DateTime.UtcNow;
        var budget = await _context.Budgets.AsNoTracking().SingleOrDefaultAsync(item =>
            item.UserId == userId && item.Month == now.Month && item.Year == now.Year, cancellationToken);
        return budget is null ? (0m, 0m) : (0m, Math.Max(0m, budget.MonthlyAmount - budget.CurrentSpending));
    }

    private static RecommendationMatch GetHighestPriorityMatch(
        Product product,
        UserPreference preferences,
        IReadOnlyDictionary<string, int> storeCounts,
        IReadOnlyDictionary<string, int> categoryCounts,
        IReadOnlyList<string> recentSearches,
        (decimal Min, decimal Max) priceRange)
    {
        // 100,000 / 10,000 / 1,000 / 100 / 1 are non-overlapping priority bands.
        if (storeCounts.TryGetValue(product.StoreName, out var storeCount))
            return new(100_000 + Math.Min(storeCount, 99), "You've bought from this store before", "favorite_store");

        var favoriteStore = preferences.FavoriteStores.FirstOrDefault(value =>
            value.Equals(product.StoreName, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(favoriteStore))
            return new(100_000, $"Matches your favorite store: {favoriteStore}", "favorite_store");

        if (categoryCounts.TryGetValue(product.Category, out var categoryCount))
            return new(10_000 + Math.Min(categoryCount, 99), $"Based on your {product.Category} purchases", "purchase_history");

        var color = preferences.FavoriteColors.FirstOrDefault(value =>
            !string.IsNullOrWhiteSpace(product.Color) && value.Equals(product.Color, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(color))
            return new(1_000, $"Matches your favorite color: {color}", "favorite_color");

        var style = preferences.FavoriteStyles.FirstOrDefault(value => TextMatches(product, value));
        if (!string.IsNullOrWhiteSpace(style))
            return new(1_000, $"Matches your favorite style: {style}", "favorite_style");

        var search = recentSearches.FirstOrDefault(value => SearchMatches(product, value));
        if (!string.IsNullOrWhiteSpace(search))
            return new(1_000, $"Related to your recent search: {DisplaySearch(search)}", "recent_search");

        if (priceRange.Max > 0 && product.Price >= priceRange.Min && product.Price <= priceRange.Max)
            return new(100, "Within your preferred price range", "price_range");

        return new(0, "Popular with other students", "popular");
    }

    private static List<PurchaseSignal> ReadPurchaseSignals(
        IEnumerable<PurchaseHistory> purchases, IReadOnlyDictionary<int, Product> products)
    {
        var result = new List<PurchaseSignal>();
        foreach (var purchase in purchases)
        {
            try
            {
                foreach (var item in JsonSerializer.Deserialize<List<PurchaseItemSnapshotDto>>(purchase.Items) ?? [])
                {
                    products.TryGetValue(item.ProductId, out var product);
                    result.Add(new PurchaseSignal(item.ProductId,
                        product?.StoreName ?? item.StoreName,
                        product?.Category ?? item.Category));
                }
            }
            catch (JsonException)
            {
                // One malformed historical snapshot should not hide all recommendations.
            }
        }
        return result.Where(item => !string.IsNullOrWhiteSpace(item.StoreName) || !string.IsNullOrWhiteSpace(item.Category)).ToList();
    }

    private static Dictionary<string, int> Count(IEnumerable<string> values) => values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

    private static bool TextMatches(Product product, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var text = $"{product.Name} {product.Description} {product.Category} {product.Color}";
        return text.Contains(value.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool SearchMatches(Product product, string search)
    {
        var term = DisplaySearch(search);
        if (TextMatches(product, term)) return true;
        var text = $"{product.Name} {product.Description} {product.Category} {product.StoreName}";
        var words = term.Split([' ', ',', '-', ':'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(word => word.Length >= 3 && !StopWords.Contains(word)).ToList();
        return words.Count > 0 && words.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsUsefulSearch(string? search) => !string.IsNullOrWhiteSpace(search) &&
        !search.StartsWith("Store visit:", StringComparison.OrdinalIgnoreCase);

    private static string DisplaySearch(string search) => search.Replace("Store visit:", "", StringComparison.OrdinalIgnoreCase).Trim();

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    { "the", "and", "for", "with", "near", "from", "deals", "best", "shop", "products" };

    private sealed record PurchaseSignal(int ProductId, string StoreName, string Category);
    private sealed record RecommendationMatch(int Score, string Reason, string ReasonType);
}
