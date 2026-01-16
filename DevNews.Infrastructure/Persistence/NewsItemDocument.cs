using DevNews.Domain.NewsItem;
using DevNews.Domain.NewsItem.Enums;
using DevNews.Domain.NewsItem.ValueObjects;

namespace DevNews.Infrastructure.Persistence;

/// <summary>
/// Cosmos DB persistence model for NewsItem.
/// Keeps domain model clean from persistence concerns.
/// </summary>
public class NewsItemDocument
{
    public string id { get; set; } = null!;
    public string Key { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Summary { get; set; } = null!;
    public string Url { get; set; } = null!;
    public int Category { get; set; }
    public int RelevanceScore { get; set; }
    public string? Source { get; set; }
    public string? Author { get; set; }
    public int? Severity { get; set; }
    public List<string> Tags { get; set; } = new();
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public static NewsItemDocument FromDomain(NewsItem newsItem)
    {
        // Compute partition key: Category_YYYY-MM (persistence concern, not domain)
        var keyDate = newsItem.PublishedAt ?? newsItem.CreatedAt;
        var partitionKey = $"{newsItem.Category.Value}_{keyDate:yyyy-MM}";

        return new NewsItemDocument
        {
            id = newsItem.Id.ToString(),
            Key = partitionKey,
            Title = newsItem.Title.Value,
            Summary = newsItem.Summary.Value,
            Url = newsItem.Url.Value,
            Category = (int)newsItem.Category.Value,
            RelevanceScore = newsItem.RelevanceScore.Value,
            Source = newsItem.Source,
            Author = newsItem.Author,
            Severity = newsItem.Severity.HasValue ? (int)newsItem.Severity.Value : null,
            Tags = newsItem.Tags.ToList(),
            PublishedAt = newsItem.PublishedAt,
            CreatedAt = newsItem.CreatedAt,
            UpdatedAt = newsItem.UpdatedAt
        };
    }

    public NewsItem ToDomain()
    {
        return NewsItem.Reconstitute(
            id: Guid.Parse(id),
            title: Title,
            summary: Summary,
            url: Url,
            category: (CategoryEnum)Category,
            relevanceScore: RelevanceScore,
            source: Source,
            author: Author,
            severity: Severity.HasValue ? (SeverityEnum)Severity.Value : null,
            tags: Tags,
            publishedAt: PublishedAt,
            createdAt: CreatedAt,
            updatedAt: UpdatedAt
        );
    }
}
