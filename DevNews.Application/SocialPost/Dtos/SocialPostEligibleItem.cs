namespace DevNews.Application.SocialPost.Dtos;

public record SocialPostEligibleItem(
    Guid NewsItemId,
    string Title,
    string Summary,
    string Category,
    int RelevanceScore,
    IReadOnlyList<string> Tags,
    string SourceUrl);
