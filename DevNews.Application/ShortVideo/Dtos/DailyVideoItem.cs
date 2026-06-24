namespace DevNews.Application.ShortVideo.Dtos;

public record DailyVideoItem(
    Guid NewsItemId,
    string Title,
    string Summary,
    string Category,
    int RelevanceScore);
