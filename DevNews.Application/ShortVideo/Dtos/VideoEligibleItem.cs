namespace DevNews.Application.ShortVideo.Dtos;

/// <summary>
/// A news item eligible for short video generation.
/// </summary>
public record VideoEligibleItem(
    Guid NewsItemId,
    string Title,
    string Summary,
    string Category,
    int RelevanceScore,
    IReadOnlyList<string> Tags);
