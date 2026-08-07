using System.Security.Claims;
using AIShoppingAssistant.Data;
using AIShoppingAssistant.DTOs;
using AIShoppingAssistant.Models;
using AIShoppingAssistant.Services;
using AIShoppingAssistant.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIShoppingAssistant.Controllers;

[Authorize]
public class PreferencesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PreferencesController> _logger;
    private readonly IPersonalizedRecommendation _recommendations;

    public PreferencesController(
        ApplicationDbContext context,
        IHttpClientFactory httpClientFactory,
        ILogger<PreferencesController> logger,
        IPersonalizedRecommendation recommendations)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _recommendations = recommendations;
    }

    [HttpGet("/Preferences/Index")]
    public async Task<IActionResult> Index()
    {
        if (!TryGetAuthenticatedUserId(out var userId))
        {
            return Unauthorized();
        }

        var preference = await GetOrCreateUserPreferenceAsync(userId);
        var recentSearches = await _context.SearchHistories
            .Where(history => history.UserId == userId)
            .OrderByDescending(history => history.SearchDate)
            .Take(8)
            .ToListAsync();

        var viewModel = new PreferencesIndexViewModel
        {
            Preferences = MapPreference(preference),
            RecentSearchHistory = recentSearches,
            SmartSuggestions = BuildSmartSuggestions(preference, recentSearches)
        };

        return View(viewModel);
    }

    [HttpPost("/Preferences/Update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateForm(UserPreferenceDto preferenceDto, CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedUserId(out _))
        {
            return Unauthorized();
        }

        var client = _httpClientFactory.CreateClient();
        var apiUrl = $"{Request.Scheme}://{Request.Host}/api/preferences/update";
        ForwardRequestCookies(client);

        var response = await client.PostAsJsonAsync(apiUrl, preferenceDto, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (Request.Headers.Accept.Any(value => value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true))
        {
            return new ContentResult
            {
                Content = body,
                ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                StatusCode = (int)response.StatusCode
            };
        }

        TempData[response.IsSuccessStatusCode ? "SuccessMessage" : "ErrorMessage"] =
            response.IsSuccessStatusCode ? "Preferences saved." : "Could not save preferences.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("/api/preferences")]
    public async Task<ActionResult<UserPreferenceDto>> Get()
    {
        try
        {
            if (!TryGetAuthenticatedUserId(out var userId))
            {
                return Unauthorized(new { message = "Invalid authentication session." });
            }

            var preference = await GetOrCreateUserPreferenceAsync(userId);
            return Ok(MapPreference(preference));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve user preferences.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Could not retrieve preferences." });
        }
    }

    [HttpPost("/api/preferences/update")]
    [HttpPut("/api/preferences/update")]
    public async Task<ActionResult<UserPreferenceDto>> Update([FromBody] UserPreferenceDto preferenceDto)
    {
        try
        {
            if (preferenceDto.PreferredPriceRangeMin > preferenceDto.PreferredPriceRangeMax)
            {
                return BadRequest(new { message = "PreferredPriceRangeMin cannot be greater than PreferredPriceRangeMax." });
            }

            if (!TryGetAuthenticatedUserId(out var userId))
            {
                return Unauthorized(new { message = "Invalid authentication session." });
            }

            var preference = await GetOrCreateUserPreferenceAsync(userId);
            preference.FavoriteStyles = NormalizeDistinctList(preferenceDto.FavoriteStyles);
            preference.FavoriteColors = NormalizeDistinctList(preferenceDto.FavoriteColors);
            preference.FavoriteStores = NormalizeDistinctList(preferenceDto.FavoriteStores);
            preference.PreferredPriceRangeMin = preferenceDto.PreferredPriceRangeMin;
            preference.PreferredPriceRangeMax = preferenceDto.PreferredPriceRangeMax;

            await _context.SaveChangesAsync();
            _recommendations.InvalidateUserCache(userId);

            return Ok(MapPreference(preference));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update user preferences.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Could not update preferences." });
        }
    }

    [HttpPost("/api/preferences/add-favorite-store")]
    public async Task<ActionResult<UserPreferenceDto>> AddFavoriteStore([FromBody] FavoriteStoreDto favoriteStoreDto)
    {
        try
        {
            if (!TryGetAuthenticatedUserId(out var userId))
            {
                return Unauthorized(new { message = "Invalid authentication session." });
            }

            var normalizedStoreName = favoriteStoreDto.StoreName.Trim();

            if (string.IsNullOrWhiteSpace(normalizedStoreName))
            {
                return BadRequest(new { message = "Store name is required." });
            }

            var preference = await GetOrCreateUserPreferenceAsync(userId);

            if (!preference.FavoriteStores.Contains(normalizedStoreName, StringComparer.OrdinalIgnoreCase))
            {
                preference.FavoriteStores.Add(normalizedStoreName);
                preference.FavoriteStores = NormalizeDistinctList(preference.FavoriteStores);
                await _context.SaveChangesAsync();
                _recommendations.InvalidateUserCache(userId);
            }

            return Ok(MapPreference(preference));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add favorite store.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Could not add favorite store." });
        }
    }

    [HttpDelete("/api/preferences/remove-favorite-store")]
    public async Task<ActionResult<UserPreferenceDto>> RemoveFavoriteStore([FromBody] FavoriteStoreDto favoriteStoreDto)
    {
        try
        {
            if (!TryGetAuthenticatedUserId(out var userId))
            {
                return Unauthorized(new { message = "Invalid authentication session." });
            }

            var normalizedStoreName = favoriteStoreDto.StoreName.Trim();

            if (string.IsNullOrWhiteSpace(normalizedStoreName))
            {
                return BadRequest(new { message = "Store name is required." });
            }

            var preference = await GetOrCreateUserPreferenceAsync(userId);
            var removedCount = preference.FavoriteStores.RemoveAll(store =>
                string.Equals(store, normalizedStoreName, StringComparison.OrdinalIgnoreCase));

            if (removedCount > 0)
            {
                await _context.SaveChangesAsync();
                _recommendations.InvalidateUserCache(userId);
            }

            return Ok(MapPreference(preference));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove favorite store.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Could not remove favorite store." });
        }
    }

    private bool TryGetAuthenticatedUserId(out int userId)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out userId);
    }

    private void ForwardRequestCookies(HttpClient client)
    {
        if (Request.Headers.TryGetValue("Cookie", out var cookies))
        {
            client.DefaultRequestHeaders.Add("Cookie", cookies.ToString());
        }
    }

    private async Task<UserPreference> GetOrCreateUserPreferenceAsync(int userId)
    {
        var preference = await _context.UserPreferences.SingleOrDefaultAsync(existingPreference => existingPreference.UserId == userId);

        if (preference is not null)
        {
            return preference;
        }

        preference = new UserPreference
        {
            UserId = userId
        };

        _context.UserPreferences.Add(preference);
        await _context.SaveChangesAsync();

        return preference;
    }

    private static List<string> NormalizeDistinctList(IEnumerable<string>? values)
    {
        return values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? new List<string>();
    }

    private static UserPreferenceDto MapPreference(UserPreference preference)
    {
        return new UserPreferenceDto
        {
            FavoriteStyles = preference.FavoriteStyles,
            FavoriteColors = preference.FavoriteColors,
            FavoriteStores = preference.FavoriteStores,
            PreferredPriceRangeMin = preference.PreferredPriceRangeMin,
            PreferredPriceRangeMax = preference.PreferredPriceRangeMax
        };
    }

    private static List<string> BuildSmartSuggestions(UserPreference preference, IReadOnlyCollection<SearchHistory> recentSearches)
    {
        var suggestions = new List<string>();

        var frequentSearchTerms = recentSearches
            .GroupBy(history => history.SearchTerm.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .Take(3)
            .ToList();

        foreach (var term in frequentSearchTerms)
        {
            suggestions.Add($"Add \"{term}\" as a style or shopping interest based on your recent searches.");
        }

        if (preference.FavoriteStores.Count == 0)
        {
            suggestions.Add("Choose a few favorite stores so product recommendations can prioritize familiar retailers.");
        }

        if (preference.PreferredPriceRangeMax <= 0)
        {
            suggestions.Add("Set a preferred price range to help filter out products outside your budget.");
        }

        if (suggestions.Count == 0)
        {
            suggestions.Add("Your preferences look healthy. New suggestions will appear as you search and shop more.");
        }

        return suggestions;
    }
}
