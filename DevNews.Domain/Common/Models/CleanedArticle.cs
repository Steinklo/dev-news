using DevNews.Domain.NewsItem.Enums;

namespace DevNews.Domain.Common.Models;

public record CleanedArticle(
    string Title,
    string Summary,
    CategoryEnum Category,
    Uri Url,
    int RelevanceScore,
    DateTimeOffset? PublishedAt,
    SeverityEnum? Severity = null,
    IReadOnlyList<string>? Tags = null,
    string? Author = null);
