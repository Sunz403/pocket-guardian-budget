using System.Text;
using System.Text.Json;
using AIShoppingAssistant.DTOs;
using AIShoppingAssistant.Models;
using OllamaSharp;
using OllamaSharp.Models.Chat;

namespace AIShoppingAssistant.Services;

/// <summary>Local shopping AI backed by Ollama's llama3.2:3b model.</summary>
public sealed class LocalAIService : IAIService
{
    private const string Model = "llama3.2:3b";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly OllamaApiClient _ollama = new(new Uri("http://localhost:11434"), Model);
    private readonly IHttpClientFactory _httpClientFactory;

    public LocalAIService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public bool IsAvailable
    {
        get
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                var client = _httpClientFactory.CreateClient("LocalAI");
                using var response = client.GetAsync("/api/tags", timeout.Token).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                    return false;

                using var document = JsonDocument.Parse(response.Content.ReadAsStream(timeout.Token));
                return document.RootElement.TryGetProperty("models", out var models)
                    && models.EnumerateArray().Any(model =>
                        model.TryGetProperty("name", out var name)
                        && string.Equals(name.GetString(), Model, StringComparison.OrdinalIgnoreCase));
            }
            catch (HttpRequestException)
            {
                return false;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
            catch (JsonException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public async Task<RecommendationResult> GetRecommendationAsync(
        List<Product> products,
        decimal userBudget,
        UserPreference preferences,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(preferences);

        var productLines = products.Take(8).Select(p =>
            $"{p.Id}|{Trim(p.Name, 45)}|{p.Price:0.##}|{Trim(p.Category, 24)}|{Trim(p.Color ?? "", 16)}|{Trim(p.Size ?? "", 12)}");
        var prompt = $$"""
            Recommend up to 3 products. Budget is {{userBudget:0.##}}. Preferences: styles={{string.Join(',', preferences.FavoriteStyles.Take(4))}}; colors={{string.Join(',', preferences.FavoriteColors.Take(4))}}; stores={{string.Join(',', preferences.FavoriteStores.Take(3))}}; price={{preferences.PreferredPriceRangeMin:0.##}}-{{preferences.PreferredPriceRangeMax:0.##}}.
            Products (id|name|price|category|color|size):
            {{string.Join('\n', productLines)}}
            Return JSON only: {"recommendations":[{"productId":number,"name":"string","reason":"short string","score":0}],"summary":"short string"}. Only use listed IDs. Prefer total item price within budget.
            """;

        return await ChatJsonAsync<RecommendationResult>(prompt, cancellationToken);
    }

    public async Task<ParsedQuery> ParseNaturalLanguageQueryAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var prompt = $$"""
            Extract shopping filters from this query: {{query}}
            Return JSON only with exactly: {"keyword":string|null,"maxPrice":number|null,"color":string|null,"category":string|null,"size":string|null}.
            maxPrice must be a number without currency. Infer nothing; use null when absent. "R" means South African rand.
            """;

        return await ChatJsonAsync<ParsedQuery>(prompt, cancellationToken);
    }

    public async Task<string> GetChatResponseAsync(
        string userMessage,
        IEnumerable<ChatHistoryMessage>? chatHistory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);

        var messages = new List<Message>
        {
            new("system", "You are a concise, helpful shopping assistant. Do not invent products, prices, or store availability.")
        };
        foreach (var item in chatHistory?.TakeLast(12) ?? Enumerable.Empty<ChatHistoryMessage>())
        {
            if (!string.IsNullOrWhiteSpace(item.Content) && IsChatRole(item.Role))
                messages.Add(new(item.Role, item.Content));
        }
        messages.Add(new("user", userMessage));

        return await SendChatAsync(messages, jsonResponse: false, cancellationToken);
    }

    private async Task<T> ChatJsonAsync<T>(string prompt, CancellationToken cancellationToken) where T : class
    {
        var response = await SendChatAsync(
            [new Message("system", "Follow the requested schema exactly. Output valid JSON only; no Markdown."), new Message("user", prompt)],
            jsonResponse: true,
            cancellationToken);
        try
        {
            return JsonSerializer.Deserialize<T>(StripCodeFence(response), JsonOptions)
                ?? throw new InvalidOperationException("Ollama returned an empty JSON response.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Ollama returned invalid JSON.", ex);
        }
    }

    private async Task<string> SendChatAsync(List<Message> messages, bool jsonResponse, CancellationToken cancellationToken)
    {
        var request = new ChatRequest
        {
            Model = Model,
            Messages = messages,
            Format = jsonResponse ? "json" : null,
            Stream = true
        };
        var response = new StringBuilder();
        try
        {
            await foreach (var chunk in _ollama.ChatAsync(request, cancellationToken))
                response.Append(chunk?.Message?.Content);
        }
        catch (HttpRequestException ex)
        {
            throw new LocalAIUnavailableException(ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LocalAIUnavailableException(ex);
        }

        return response.Length > 0
            ? response.ToString()
            : throw new InvalidOperationException("Ollama returned an empty chat response.");
    }

    private static bool IsChatRole(string role) => role is "user" or "assistant" or "system";
    private static string Trim(string value, int max) => value.Length <= max ? value : value[..max];

    private static string StripCodeFence(string value) => value.Trim()
        .Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
        .Replace("```", string.Empty, StringComparison.Ordinal)
        .Trim();
}
