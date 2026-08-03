using System.Security.Claims;
using AIShoppingAssistant.DTOs;
using AIShoppingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIShoppingAssistant.Controllers;

[ApiController]
[Authorize]
[Route("api/recommendations")]
public sealed class RecommendationsController : ControllerBase
{
    private readonly RecommendationService _recommendations;

    public RecommendationsController(RecommendationService recommendations) => _recommendations = recommendations;

    [HttpGet("infinite")]
    [ProducesResponseType(typeof(InfiniteRecommendationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<InfiniteRecommendationResponseDto>> Infinite(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? category = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) return BadRequest(new { message = "page must be at least 1." });
        if (pageSize is < 1 or > 50) return BadRequest(new { message = "pageSize must be between 1 and 50." });
        if (minPrice.HasValue && maxPrice.HasValue && minPrice > maxPrice)
            return BadRequest(new { message = "minPrice cannot be greater than maxPrice." });

        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdValue, out var userId)) return Unauthorized();

        return Ok(await _recommendations.GetInfiniteRecommendationsAsync(
            userId, page, pageSize, category, minPrice, maxPrice, cancellationToken));
    }
}
