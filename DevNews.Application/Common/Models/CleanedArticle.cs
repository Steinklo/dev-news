using DevNews.Domain.NewsItem.Enums;

namespace DevNews.Application.Common.Models;

/// <summary>
/// Represents a curated article ready for persistence.
/// Output from the curation service.
/// </summary>
public record CleanedArticle(
    string Title,
    string Summary,
    CategoryEnum Category,
    Uri Url,
    int RelevanceScore,
    DateTimeOffset? PublishedAt,
    string? Source = null,
    string? Author = null,
    SeverityEnum? Severity = null,
    IReadOnlyList<string>? Tags = null);
