using System.Diagnostics;
using AIShoppingAssistant.DTOs;
using AIShoppingAssistant.Models;
using AIShoppingAssistant.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIShoppingAssistant.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestAIController : ControllerBase
{
    private const string ModelName = "llama3.2:3b";

    private readonly LocalAIService _localAiService;
    private readonly ILogger<TestAIController> _logger;

    public TestAIController(LocalAIService localAiService, ILogger<TestAIController> logger)
    {
        _localAiService = localAiService;
        _logger = logger;
    }

    [HttpGet("health")]
    public ActionResult GetHealth()
    {
        var stopwatch = Stopwatch.StartNew();
        var isRunning = _localAiService.IsAvailable;
        stopwatch.Stop();

        return Ok(new
        {
            modelStatus = isRunning ? "running" : "not running",
            modelName = ModelName,
            responseTime = $"{stopwatch.ElapsedMilliseconds}ms",
            availableMethods = new[]
            {
                "GET /api/testai/health",
                "POST /api/testai/chat",
                "POST /api/testai/parse",
                "POST /api/testai/recommend"
            }
        });
    }

    [HttpPost("chat")]
    public async Task<ActionResult> Chat([FromBody] TestAiChatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { message = "Message is required." });
        }

        try
        {
            var response = await _localAiService.GetChatResponseAsync(
                request.Message,
                chatHistory: null,
                cancellationToken);

            return Ok(new
            {
                message = request.Message,
                response
            });
        }
        catch (LocalAIUnavailableException ex)
        {
            _logger.LogWarning(ex, "Local AI service is unavailable during chat test.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Local AI service is unavailable.",
                detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat test failed.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Chat test failed.",
                detail = ex.Message
            });
        }
    }

    [HttpPost("parse")]
    public async Task<ActionResult> Parse([FromBody] TestAiParseRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { message = "Query is required." });
        }

        try
        {
            var parsedQuery = await _localAiService.ParseNaturalLanguageQueryAsync(
                request.Query,
                cancellationToken);

            return Ok(new
            {
                query = request.Query,
                parsedQuery
            });
        }
        catch (LocalAIUnavailableException ex)
        {
            _logger.LogWarning(ex, "Local AI service is unavailable during parse test.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Local AI service is unavailable.",
                detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Parse test failed.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Parse test failed.",
                detail = ex.Message
            });
        }
    }

    [HttpPost("recommend")]
    public async Task<ActionResult> Recommend([FromBody] TestAiRecommendRequest request, CancellationToken cancellationToken)
    {
        if (request.Budget <= 0)
        {
            return BadRequest(new { message = "Budget must be greater than zero." });
        }

        try
        {
            var sampleProducts = BuildSampleProducts();
            var preferences = new UserPreference
            {
                FavoriteColors = request.Preferences.Colors,
                FavoriteStyles = request.Preferences.Styles,
                FavoriteStores = new List<string> { "SportScene", "Takealot", "Superbalist" },
                PreferredPriceRangeMin = 0,
                PreferredPriceRangeMax = request.Budget
            };

            var recommendation = await _localAiService.GetRecommendationAsync(
                sampleProducts,
                request.Budget,
                preferences,
                cancellationToken);

            return Ok(new
            {
                budget = request.Budget,
                preferences = request.Preferences,
                sampleProducts,
                recommendation
            });
        }
        catch (LocalAIUnavailableException ex)
        {
            _logger.LogWarning(ex, "Local AI service is unavailable during recommendation test.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Local AI service is unavailable.",
                detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Recommendation test failed.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Recommendation test failed.",
                detail = ex.Message
            });
        }
    }

    private static List<Product> BuildSampleProducts()
    {
        return new List<Product>
        {
            new()
            {
                Id = 1,
                Name = "Velocity Red Runner",
                Description = "Lightweight red running shoe for daily training.",
                Price = 449.99m,
                Color = "red",
                Size = "8",
                ShippingCost = 0m,
                StoreName = "SportScene",
                Category = "running"
            },
            new()
            {
                Id = 2,
                Name = "Sprint Flex Trainer",
                Description = "Neutral running trainer with breathable mesh.",
                Price = 499.99m,
                Color = "red",
                Size = "9",
                ShippingCost = 0m,
                StoreName = "Takealot",
                Category = "running"
            },
            new()
            {
                Id = 3,
                Name = "Urban Street Sneaker",
                Description = "Casual sneaker with red accents.",
                Price = 379.99m,
                Color = "red",
                Size = "8",
                ShippingCost = 50m,
                StoreName = "Superbalist",
                Category = "casual"
            },
            new()
            {
                Id = 4,
                Name = "Trail Peak Runner",
                Description = "Trail shoe with extra grip for outdoor runs.",
                Price = 589.99m,
                Color = "black",
                Size = "10",
                ShippingCost = 0m,
                StoreName = "SportScene",
                Category = "running"
            }
        };
    }
}
