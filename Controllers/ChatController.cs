using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using AIShoppingAssistant.Data;
using AIShoppingAssistant.DTOs;
using AIShoppingAssistant.Models;
using AIShoppingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIShoppingAssistant.Controllers;

[ApiController]
[Authorize]
[Route("api/chat")]
public sealed class ChatController : ControllerBase
{
    private const string SessionCookie = "AIShopping.ChatSession";
    private readonly ApplicationDbContext _db;
    private readonly LocalAIService _ai;
    private readonly ILogger<ChatController> _logger;

    public ChatController(ApplicationDbContext db, LocalAIService ai, ILogger<ChatController> logger)
    {
        _db = db;
        _ai = ai;
        _logger = logger;
    }

    [HttpGet("history")]
    public async Task<ActionResult<object>> History(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var session = await GetCurrentSessionAsync(userId, create: false, cancellationToken);
        var messages = session is null ? [] : await _db.ChatMessages.AsNoTracking()
            .Where(message => message.UserId == userId && message.ChatSessionId == session.Id)
            .OrderBy(message => message.Timestamp)
            .Select(message => ToDto(message))
            .ToListAsync(cancellationToken);
        return Ok(new { sessionId = session?.Id, messages });
    }

    [HttpPost("send")]
    public async Task<ActionResult<ChatResponseDto>> Send([FromBody] SendChatMessageDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message)) return BadRequest(new { message = "A message is required." });

        try
        {
        var userId = GetUserId();
        var session = await GetCurrentSessionAsync(userId, create: true, cancellationToken);
        var text = request.Message.Trim();
        var userMessage = new ChatMessage { UserId = userId, ChatSessionId = session!.Id, Sender = "User", Message = text };
        _db.ChatMessages.Add(userMessage);
        await _db.SaveChangesAsync(cancellationToken);

        var priorMessages = await _db.ChatMessages.AsNoTracking()
            .Where(message => message.ChatSessionId == session.Id && message.Id != userMessage.Id)
            .OrderByDescending(message => message.Timestamp)
            .Take(24)
            .OrderBy(message => message.Timestamp)
            .Select(message => new ChatHistoryMessage
            {
                Role = message.Sender == "AI" ? "assistant" : "user",
                Content = message.Message
            })
            .ToListAsync(cancellationToken);

        var products = await FindRelevantProductsAsync(text, cancellationToken);
        Product? changedProduct = null;
        string? shoppingListAction = null;
        if (IsSelectionRequest(text) && products.Count > 0)
        {
            changedProduct = await SelectItemAsync(userId, products[0], cancellationToken);
            if (changedProduct is not null) shoppingListAction = "added to the shopping list";
        }
        else if (IsRemovalRequest(text))
        {
            changedProduct = await RemoveItemAsync(userId, text, cancellationToken);
            if (changedProduct is not null) shoppingListAction = "removed from the shopping list";
        }
        var context = await BuildContextAsync(userId, products, changedProduct, shoppingListAction, cancellationToken);
        priorMessages.Insert(0, new ChatHistoryMessage { Role = "system", Content = context });

        string reply;
        try
        {
            reply = await _ai.GetChatResponseAsync(text, priorMessages, cancellationToken);
        }
        catch (LocalAIUnavailableException)
        {
            reply = BuildHelpfulFallback(products);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat response failed for user {UserId}", userId);
            reply = "I couldn't complete that just now. Please try again in a moment.";
        }

        var aiMessage = new ChatMessage { UserId = userId, ChatSessionId = session.Id, Sender = "AI", Message = reply };
        _db.ChatMessages.Add(aiMessage);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new ChatResponseDto
        {
            SessionId = session.Id,
            UserMessage = ToDto(userMessage),
            AiMessage = ToDto(aiMessage),
            Products = products.Select(product => new ChatProductDto
            {
                Id = product.Id, Name = product.Name, StoreName = product.StoreName, Price = product.Price, Description = product.Description
            }).ToList()
        });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat request failed before the assistant could respond.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "The AI assistant cannot reach its data source right now. Please try again shortly." });
        }
    }

    [HttpDelete("clear")]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var session = await GetCurrentSessionAsync(userId, create: false, cancellationToken);
        if (session is not null)
        {
            var messages = _db.ChatMessages.Where(message => message.UserId == userId && message.ChatSessionId == session.Id);
            _db.ChatMessages.RemoveRange(messages);
            session.EndedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
        Response.Cookies.Delete(SessionCookie);
        return NoContent();
    }

    private async Task<ChatSession?> GetCurrentSessionAsync(int userId, bool create, CancellationToken cancellationToken)
    {
        var sessionId = Request.Cookies[SessionCookie];
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            var existing = await _db.ChatSessions.SingleOrDefaultAsync(session => session.Id == sessionId && session.UserId == userId && session.EndedAt == null, cancellationToken);
            if (existing is not null) return existing;
        }
        if (!create) return null;

        var session = new ChatSession { UserId = userId };
        _db.ChatSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);
        Response.Cookies.Append(SessionCookie, session.Id, new CookieOptions { HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(30) });
        return session;
    }

    private async Task<string> BuildContextAsync(int userId, IReadOnlyCollection<Product> products, Product? changedProduct, string? shoppingListAction, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var budget = await _db.Budgets.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId && item.Month == now.Month && item.Year == now.Year, cancellationToken);
        var preferences = await _db.UserPreferences.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        var shoppingList = await _db.ShoppingListItems.AsNoTracking().Where(item => item.UserId == userId).OrderBy(item => item.SelectedDate).Select(item => new { item.ProductName, item.Price }).ToListAsync(cancellationToken);
        var recentPurchases = await _db.PurchaseHistories.AsNoTracking().Where(item => item.UserId == userId).OrderByDescending(item => item.PurchaseDate).Take(3).Select(item => new { item.PurchaseDate, item.TotalAmount }).ToListAsync(cancellationToken);
        var builder = new StringBuilder("You are a friendly South African shopping and budgeting assistant. Be concise, practical and use R for rand. Never invent product prices, stores, availability, links or actions. ");
        builder.Append(budget is null ? "No monthly budget is set. " : $"Monthly budget: R{budget.MonthlyAmount:0.00}; spent: R{budget.CurrentSpending:0.00}; remaining: R{Math.Max(0, budget.MonthlyAmount - budget.CurrentSpending):0.00}. ");
        if (preferences is not null) builder.Append($"Preferences: stores {string.Join(", ", preferences.FavoriteStores.Take(4))}; styles/categories {string.Join(", ", preferences.FavoriteStyles.Take(4))}; colours {string.Join(", ", preferences.FavoriteColors.Take(4))}. ");
        if (shoppingList.Count > 0) builder.Append($"Shopping list: {string.Join(" | ", shoppingList.Select(item => $"{item.ProductName} (R{item.Price:0.00})"))}. ");
        if (recentPurchases.Count > 0) builder.Append($"Recent purchase totals: {string.Join(", ", recentPurchases.Select(p => $"R{p.TotalAmount:0.00}"))}. ");
        if (products.Count > 0) builder.Append("Relevant catalog products: " + string.Join(" | ", products.Select(p => $"#{p.Id} {p.Name} — R{p.Price:0.00} at {p.StoreName} ({p.Category})")) + ". ");
        if (changedProduct is not null && shoppingListAction is not null) builder.Append($"The user explicitly requested it and {changedProduct.Name} has just been {shoppingListAction}. Confirm this plainly. ");
        return builder.ToString();
    }

    private async Task<List<Product>> FindRelevantProductsAsync(string message, CancellationToken cancellationToken)
    {
        var lower = message.ToLowerInvariant();
        if (!new[] { "find", "product", "shoe", "grocery", "groceries", "running", "nike", "recommend", "under", "add", "select", "take", "list", "r" }.Any(lower.Contains)) return [];
        var query = _db.Products.AsNoTracking();
        var priceMatch = Regex.Match(message, @"(?:under|below|less than|r)\s*R?\s*(\d+(?:[,.]\d+)?)", RegexOptions.IgnoreCase);
        if (priceMatch.Success && decimal.TryParse(priceMatch.Groups[1].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var maxPrice)) query = query.Where(product => product.Price <= maxPrice);
        var terms = Regex.Matches(lower, @"[a-z]{4,}").Select(match => match.Value).Where(term => term is not "what" and not "about" and not "best" and not "find" and not "with" and not "that" and not "under" and not "product" and not "products" and not "recommend").Take(5).ToList();
        foreach (var term in terms) query = query.Where(product => product.Name.ToLower().Contains(term) || product.Category.ToLower().Contains(term) || (product.Description != null && product.Description.ToLower().Contains(term)) || product.StoreName.ToLower().Contains(term));
        var matches = await query.OrderBy(product => product.Price).Take(3).ToListAsync(cancellationToken);
        return matches.Count > 0 ? matches : await _db.Products.AsNoTracking().OrderBy(product => product.Price).Take(3).ToListAsync(cancellationToken);
    }

    private static string BuildHelpfulFallback(IReadOnlyCollection<Product> products) => products.Count == 0
        ? "I can help with budgeting, saving tips, comparing products, or finding something within a price range. What would you like to explore?"
        : "I found a few options from the catalog below. You can select any of them for your shopping list, and I can help you compare their value against your budget.";

    private static bool IsSelectionRequest(string message) => Regex.IsMatch(message, @"^\s*(?:add|select|i(?:'m| am)?\s+(?:going to\s+)?(?:take|choose)|i(?:'ll| will)\s+(?:take|choose)|put)\b", RegexOptions.IgnoreCase);

    private static bool IsRemovalRequest(string message) => Regex.IsMatch(message, @"^\s*(?:remove|delete|take\s+off|drop)\b", RegexOptions.IgnoreCase);

    private async Task<Product?> SelectItemAsync(int userId, Product product, CancellationToken cancellationToken)
    {
        var item = await _db.ShoppingListItems.SingleOrDefaultAsync(item => item.UserId == userId && item.ProductId == product.Id, cancellationToken);
        if (item is not null) return product;
        _db.ShoppingListItems.Add(new ShoppingListItem { UserId = userId, ProductId = product.Id, ProductName = product.Name, Price = product.Price, SelectedDate = DateTime.UtcNow });
        await _db.SaveChangesAsync(cancellationToken);
        return product;
    }

    private async Task<Product?> RemoveItemAsync(int userId, string message, CancellationToken cancellationToken)
    {
        var items = await _db.ShoppingListItems.Where(item => item.UserId == userId).ToListAsync(cancellationToken);
        if (items.Count == 0) return null;
        var searchableMessage = message.ToLowerInvariant();
        var item = items.OrderByDescending(candidate => candidate.ProductName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Count(word => word.Length > 2 && searchableMessage.Contains(word.ToLowerInvariant())))
            .First();
        var product = await _db.Products.AsNoTracking().SingleOrDefaultAsync(candidate => candidate.Id == item.ProductId, cancellationToken);
        _db.ShoppingListItems.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);
        return product ?? new Product { Id = item.ProductId, Name = item.ProductName, Price = item.Price, StoreName = string.Empty, Category = string.Empty };
    }

    private int GetUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : throw new UnauthorizedAccessException();
    private static ChatMessageDto ToDto(ChatMessage message) => new() { Id = message.Id, Message = message.Message, Sender = message.Sender, Timestamp = message.Timestamp, ChatSessionId = message.ChatSessionId };
}
