using DevNews.Domain.Common;
using DevNews.Domain.NewsItem.Enums;
using DevNews.Domain.NewsItem.Events;
using DevNews.Domain.NewsItem.ValueObjects;

namespace DevNews.Domain.NewsItem;

/// <summary>
/// NewsItem Aggregate Root - represents a single piece of curated developer news
/// Lifecycle: discovered → processed → validated (relevant + unique) → stored
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

    // Timestamps
    public DateTimeOffset PublishedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    // Private constructor for EF
    private NewsItem(Guid id) : base(id)
    {
    }

    private NewsItem(
        Guid id,
        NewsTitle title,
        NewsSummary summary,
        NewsUrl url,
        NewsCategory category) : base(id)
    {
        Title = title;
        Summary = summary;
        Url = url;
        Category = category;
        PublishedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Factory method to create a validated NewsItem
    /// Enforces invariants: must be relevant, not duplicate, valid data
    /// </summary>
    public static ResultResponse<NewsItem> Create(
        string title,
        string summary,
        string url,
        CategoryEnum category)
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

        var newsItem = new NewsItem(
            id: Guid.CreateVersion7(),
            title: titleResult.Data,
            summary: summaryResult.Data,
            url: urlResult.Data,
            category: categoryResult.Data);

        newsItem._domainEvents.Add(new NewsCreatedEvent(newsItem: newsItem));

        return ResultResponse<NewsItem>.Success(newsItem);
    }

    /// <summary>
    /// Update news item properties
    /// </summary>
    public ResultResponse<NewsItem> Update(
        string title,
        string info,
        string url,
        CategoryEnum category)
    {
        var titleResult = NewsTitle.Create(title);
        if (!titleResult.IsSuccess)
            return ResultResponse<NewsItem>.Failure(titleResult.ErrorMessage);

        var summaryResult = NewsSummary.Create(info);
        if (!summaryResult.IsSuccess)
            return ResultResponse<NewsItem>.Failure(summaryResult.ErrorMessage);

        var urlResult = NewsUrl.Create(url);
        if (!urlResult.IsSuccess)
            return ResultResponse<NewsItem>.Failure(urlResult.ErrorMessage);

        var categoryResult = NewsCategory.Create(category);
        if (!categoryResult.IsSuccess)
            return ResultResponse<NewsItem>.Failure(categoryResult.ErrorMessage);

        Title = titleResult.Data;
        Summary = summaryResult.Data;
        Url = urlResult.Data;
        Category = categoryResult.Data;
        UpdatedAt = DateTimeOffset.Now;

        _domainEvents.Add(new NewsUpdatedEvent(newsItem: this));

        return ResultResponse<NewsItem>.Success(this);
    }
}