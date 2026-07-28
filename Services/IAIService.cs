using AIShoppingAssistant.DTOs;
using AIShoppingAssistant.Models;

namespace AIShoppingAssistant.Services;

public interface IAIService
{
    bool IsAvailable { get; }

    Task<RecommendationResult> GetRecommendationAsync(
        List<Product> products,
        decimal userBudget,
        UserPreference preferences,
        CancellationToken cancellationToken = default);

    Task<ParsedQuery> ParseNaturalLanguageQueryAsync(
        string query,
        CancellationToken cancellationToken = default);

    Task<string> GetChatResponseAsync(
        string userMessage,
        IEnumerable<ChatHistoryMessage>? chatHistory,
        CancellationToken cancellationToken = default);
}
