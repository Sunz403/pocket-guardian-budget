using AIShoppingAssistant.DTOs;

namespace AIShoppingAssistant.Services;

/// <summary>Provides cached, explainable product recommendations for one user.</summary>
public interface IPersonalizedRecommendation
{
    Task<IReadOnlyList<PersonalizedRecommendationDto>> GetPersonalizedRecommendations(
        int userId,
        int limit = 10,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PersonalizedRecommendationDto>> GetPersonalizedRecommendationsAsync(
        int userId,
        int limit = 10,
        CancellationToken cancellationToken = default);

    Task<InfiniteRecommendationResponseDto> GetInfiniteRecommendationsAsync(
        int userId, int page, int pageSize, string? category, decimal? minPrice, decimal? maxPrice,
        CancellationToken cancellationToken = default);

    void InvalidateUserCache(int userId);
}
