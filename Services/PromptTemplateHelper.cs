using System.Globalization;
using System.Text.Json;

namespace AIShoppingAssistant.Services;

/// <summary>
/// Compact prompt templates for the local llama3.2:3b shopping assistant.
/// </summary>
public static class PromptTemplateHelper
{
    public const string RECOMMENDATION_PROMPT = """
        You are a helpful shopping assistant. Given these products: {products_json}, user budget: {budget}, and preferences: {preferences}. Recommend the top 3 products. Return ONLY valid JSON in this format: {"recommendations": [{"productId": 1, "reason": "short reason", "confidence": 0.9}]}
        """;

    public const string PARSE_QUERY_PROMPT = """
        Extract product details from this shopping query: '{userQuery}'. Return ONLY JSON in this format: {"keyword": "", "maxPrice": 0, "color": "", "category": "", "size": ""}
        """;

    public const string CHAT_PROMPT = """
        You are a budget-savvy shopping assistant for students. User budget: {budgetContext}. User: {userMessage}. Assistant:
        """;

    /// <summary>Builds a JSON-only recommendation prompt from product and preference objects.</summary>
    public static string BuildRecommendationPrompt(object products, decimal budget, object preferences)
    {
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(preferences);

        return RECOMMENDATION_PROMPT
            .Replace("{products_json}", JsonSerializer.Serialize(products), StringComparison.Ordinal)
            .Replace("{budget}", budget.ToString("0.##", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{preferences}", JsonSerializer.Serialize(preferences), StringComparison.Ordinal);
    }

    /// <summary>Builds a JSON-only product-filter extraction prompt.</summary>
    public static string BuildParseQueryPrompt(string userQuery)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userQuery);

        return PARSE_QUERY_PROMPT.Replace("{userQuery}", userQuery.Trim(), StringComparison.Ordinal);
    }
}
