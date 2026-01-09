namespace DevNews.Domain.Common.Models;

public record CleanedArticle(
    string Title,
    string Summary,
    string Category,
    Uri Url,
    DateTimeOffset? PublishedAt,
    string? Author = null);