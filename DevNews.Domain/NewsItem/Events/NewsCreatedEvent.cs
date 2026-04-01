using DevNews.Domain.Common;

namespace DevNews.Domain.NewsItem.Events;

public class NewsCreatedEvent(
    NewsItem newsItem) : DomainEvent(newsItem.Id)
{
    public NewsItem NewsItem { get; } = newsItem;
}