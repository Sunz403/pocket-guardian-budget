using System.Text.RegularExpressions;
using AIShoppingAssistant.DTOs;

namespace AIShoppingAssistant.Services;

public static partial class ShoppingQueryNormalizer
{
    private static readonly string[] KnownColors =
    [
        "black", "blue", "brown", "cream", "gray", "green", "grey", "navy",
        "olive", "purple", "red", "silver", "tan", "white", "yellow"
    ];

    public static ParsedQuery Normalize(string query, ParsedQuery? parsed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        parsed ??= new ParsedQuery();
        parsed.Keyword = Clean(parsed.Keyword);
        parsed.Color = Clean(parsed.Color);
        parsed.Category = Clean(parsed.Category);
        parsed.Size = Clean(parsed.Size);

        parsed.MaxPrice ??= ExtractPrice(query);
        parsed.Color ??= ExtractColor(query);

        var inferred = InferIntent(query);
        parsed.Category ??= inferred.Category;
        parsed.Keyword = BestKeyword(parsed.Keyword, inferred.Keyword);

        return parsed;
    }

    public static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var cleaned = value.Trim().Trim('"', '\'');
        return cleaned.Equals("null", StringComparison.OrdinalIgnoreCase) ||
               cleaned.Equals("none", StringComparison.OrdinalIgnoreCase) ||
               cleaned.Equals("n/a", StringComparison.OrdinalIgnoreCase)
            ? null
            : cleaned;
    }

    private static string? BestKeyword(string? modelKeyword, string? inferredKeyword)
    {
        if (string.IsNullOrWhiteSpace(inferredKeyword))
            return modelKeyword;

        if (string.IsNullOrWhiteSpace(modelKeyword))
            return inferredKeyword;

        return modelKeyword.Length >= inferredKeyword.Length ? modelKeyword : inferredKeyword;
    }

    private static (string? Keyword, string? Category) InferIntent(string query)
    {
        var text = query.ToLowerInvariant();

        if (ContainsAny(text, "running shoe", "running shoes", "sneaker", "sneakers", "trainer", "trainers", "shoe", "shoes", "boots"))
            return (ContainsAny(text, "running", "runner") ? "running shoe" : "shoes", "Shoes");

        if (ContainsAny(text, "smartphone", "smart phone", "phone", "mobile"))
            return ("smartphone", "Electronics");

        if (ContainsAny(text, "groceries", "grocery", "food", "pantry", "maize", "rice", "pasta"))
            return ("groceries", "Groceries");

        if (ContainsAny(text, "formal jacket", "jacket", "blazer", "coat", "outerwear"))
            return (ContainsAny(text, "formal") ? "formal jacket" : "jacket", "Outerwear");

        return (null, null);
    }

    private static decimal? ExtractPrice(string query)
    {
        var match = PriceRegex().Match(query);
        return match.Success && decimal.TryParse(match.Groups[1].Value, out var price) ? price : null;
    }

    private static string? ExtractColor(string query)
    {
        var text = query.ToLowerInvariant();
        return KnownColors.FirstOrDefault(color => Regex.IsMatch(text, $@"\b{Regex.Escape(color)}\b", RegexOptions.IgnoreCase));
    }

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex(@"(?:R\s*)?(\d+(?:[.,]\d{1,2})?)", RegexOptions.IgnoreCase)]
    private static partial Regex PriceRegex();
}
