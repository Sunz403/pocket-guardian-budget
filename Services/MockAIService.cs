using System.Globalization;
using System.Text.RegularExpressions;
using AIShoppingAssistant.DTOs;
using AIShoppingAssistant.Models;

namespace AIShoppingAssistant.Services;

/// <summary>Deterministic fallback used when the local Ollama model is unavailable.</summary>
public sealed class MockAIService : IAIService
{
    private const string UnavailableMessage =
        "I'm sorry, the AI assistant is currently unavailable. Please try again later.";

    public bool IsAvailable => false;

    public Task<RecommendationResult> GetRecommendationAsync(
        List<Product> products,
        decimal userBudget,
        UserPreference preferences,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(preferences);

        var keywords = preferences.FavoriteStyles
            .Concat(preferences.FavoriteColors)
            .Concat(preferences.FavoriteStores)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        var candidates = products
            .Where(product => userBudget <= 0 || product.Price + product.ShippingCost <= userBudget)
            .Select(product => new
            {
                Product = product,
                Matches = keywords.Count(keyword => Matches(product, keyword))
            })
            .OrderByDescending(candidate => candidate.Matches)
            .ThenBy(candidate => candidate.Product.Price + candidate.Product.ShippingCost)
            .Take(3)
            .Select((candidate, index) => new ProductRecommendation
            {
                ProductId = candidate.Product.Id,
                Name = candidate.Product.Name,
                Reason = candidate.Matches > 0
                    ? "Matches your saved shopping preferences."
                    : "A budget-friendly option from the available products.",
                Score = Math.Max(70, 95 - (index * 10))
            })
            .ToList();

        return Task.FromResult(new RecommendationResult
        {
            Recommendations = candidates,
            Summary = candidates.Count == 0
                ? "No products currently match the selected budget."
                : "These recommendations were selected using offline keyword matching."
        });
    }

    public Task<ParsedQuery> ParseNaturalLanguageQueryAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var keyword = Regex.Match(query, @"[\p{L}\p{N}]+", RegexOptions.CultureInvariant).Value;
        var number = Regex.Match(query, @"\d+(?:[,.]\d+)?", RegexOptions.CultureInvariant).Value;
        var maxPrice = decimal.TryParse(
            number.Replace(',', '.'),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var parsedPrice)
            ? parsedPrice
            : (decimal?)null;

        return Task.FromResult(new ParsedQuery
        {
            Keyword = string.IsNullOrEmpty(keyword) ? null : keyword,
            MaxPrice = maxPrice
        });
    }

    public Task<string> GetChatResponseAsync(
        string userMessage,
        IEnumerable<ChatHistoryMessage>? chatHistory,
        CancellationToken cancellationToken = default) => Task.FromResult(UnavailableMessage);

    private static bool Matches(Product product, string keyword)
    {
        var comparison = StringComparison.OrdinalIgnoreCase;
        return product.Name.Contains(keyword, comparison)
            || product.Category.Contains(keyword, comparison)
            || product.Color?.Contains(keyword, comparison) == true
            || product.StoreName.Contains(keyword, comparison);
    }
}
