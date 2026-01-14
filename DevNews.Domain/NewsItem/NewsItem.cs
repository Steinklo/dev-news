using DevNews.Domain.Common;
using DevNews.Domain.NewsItem.Enums;
using DevNews.Domain.NewsItem.Events;
using DevNews.Domain.NewsItem.ValueObjects;

namespace DevNews.Domain.NewsItem;

/// <summary>
/// NewsItem Aggregate Root - represents a single piece of curated developer news
/// Lifecycle: discovered → processed → validated (relevant + unique) → stored
/// News items are immutable after creation.
/// </summary>
public class NewsItem : AggregateRoot<Guid>
{
    private readonly List<DomainEvent> _domainEvents = new();

    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    // Properties using Value Objects
    public NewsTitle Title { get; private set; } = null!;
    public NewsSummary Summary { get; private set; } = null!;
    public NewsUrl Url { get; private set; } = null!;
    public NewsCategory Category { get; private set; } = null!;
    public RelevanceScore RelevanceScore { get; private set; } = null!;

    // Partition key: Category_YYYY-MM (e.g., "SecurityAndVulnerabilities_2026-01")
    public string Key { get; internal set; } = null!;

    // Source of the article (e.g., "GitHub", "Hacker News", "Reddit")
    public string? Source { get; private set; }

    // Author of the article if available
    public string? Author { get; private set; }

    // Optional: only for SecurityAndVulnerabilities category
    public SeverityEnum? Severity { get; private set; }

    // Tags for filtering/search (max 5): e.g. cve, kubernetes, go1.24, breaking-change
    private readonly List<string> _tags = new();
    public IReadOnlyCollection<string> Tags => _tags.AsReadOnly();

    // Timestamps
    public DateTimeOffset? PublishedAt { get; private set; }  // When the original article was published
    public DateTimeOffset CreatedAt { get; internal set; }      // When we stored it
    public DateTimeOffset? UpdatedAt { get; internal set; }     // When we last modified it

    private NewsItem(
        Guid id,
        NewsTitle title,
        NewsSummary summary,
        NewsUrl url,
        NewsCategory category,
        RelevanceScore relevanceScore,
        string? source,
        string? author,
        SeverityEnum? severity,
        IEnumerable<string>? tags,
        DateTimeOffset? publishedAt) : base(id)
    {
        Title = title;
        Summary = summary;
        Url = url;
        Category = category;
        RelevanceScore = relevanceScore;
        Source = source;
        Author = author;
        Severity = severity;
        if (tags != null)
            _tags.AddRange(tags);
        PublishedAt = publishedAt;
        CreatedAt = DateTimeOffset.UtcNow;

        // Compute partition key: Category_YYYY-MM
        var keyDate = publishedAt ?? CreatedAt;
        Key = $"{category.Value}_{keyDate:yyyy-MM}";
    }

    /// <summary>
    /// Factory method to create a validated NewsItem
    /// Enforces invariants: must be relevant, not duplicate, valid data
    /// </summary>
    public static ResultResponse<NewsItem> Create(
        string title,
        string summary,
        string url,
        CategoryEnum category,
        int relevanceScore,
        string? source = null,
        string? author = null,
        DateTimeOffset? publishedAt = null,
        SeverityEnum? severity = null,
        IEnumerable<string>? tags = null)
    {
        var titleResult = NewsTitle.Create(title);
        if (!titleResult.IsSuccess)
            return ResultResponse<NewsItem>.Failure(titleResult.ErrorMessage);

        var summaryResult = NewsSummary.Create(summary);
        if (!summaryResult.IsSuccess)
            return ResultResponse<NewsItem>.Failure(summaryResult.ErrorMessage);

        var urlResult = NewsUrl.Create(url);
        if (!urlResult.IsSuccess)
            return ResultResponse<NewsItem>.Failure(urlResult.ErrorMessage);

        var categoryResult = NewsCategory.Create(category);
        if (!categoryResult.IsSuccess)
            return ResultResponse<NewsItem>.Failure(categoryResult.ErrorMessage);

        var relevanceResult = RelevanceScore.Create(relevanceScore);
        if (!relevanceResult.IsSuccess)
            return ResultResponse<NewsItem>.Failure(relevanceResult.ErrorMessage);

        // Validate severity is only set for security items
        if (severity.HasValue && category != CategoryEnum.SecurityAndVulnerabilities)
            return ResultResponse<NewsItem>.Failure("Severity can only be set for SecurityAndVulnerabilities category");

        var newsItem = new NewsItem(
            id: Guid.CreateVersion7(),
            title: titleResult.Data!,
            summary: summaryResult.Data!,
            url: urlResult.Data!,
            category: categoryResult.Data!,
            relevanceScore: relevanceResult.Data!,
            source: source,
            author: author,
            severity: severity,
            tags: tags,
            publishedAt: publishedAt);

        newsItem._domainEvents.Add(new NewsCreatedEvent(newsItem: newsItem));

        return ResultResponse<NewsItem>.Success(newsItem);
    }

    /// <summary>
    /// Reconstitutes a NewsItem from persistence. Bypasses validation since data was already validated on creation.
    /// </summary>
    internal static NewsItem Reconstitute(
        Guid id,
        string title,
        string summary,
        string url,
        string key,
        CategoryEnum category,
        int relevanceScore,
        string? source,
        string? author,
        SeverityEnum? severity,
        IEnumerable<string>? tags,
        DateTimeOffset? publishedAt,
        DateTimeOffset createdAt,
        DateTimeOffset? updatedAt)
    {
        var newsItem = new NewsItem(
            id: id,
            title: NewsTitle.Reconstitute(title),
            summary: NewsSummary.Reconstitute(summary),
            url: NewsUrl.Reconstitute(url),
            category: NewsCategory.Reconstitute(category),
            relevanceScore: RelevanceScore.Reconstitute(relevanceScore),
            source: source,
            author: author,
            severity: severity,
            tags: tags,
            publishedAt: publishedAt);

        newsItem.Key = key;
        newsItem.CreatedAt = createdAt;
        newsItem.UpdatedAt = updatedAt;

        return newsItem;
    }
}
