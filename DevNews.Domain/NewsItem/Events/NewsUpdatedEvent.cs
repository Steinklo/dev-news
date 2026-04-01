using DevNews.Domain.Common;

namespace DevNews.Domain.NewsItem.Events;

public class NewsUpdatedEvent(
    NewsItem newsItem) : DomainEvent(newsItem.Id)
{
    public NewsItem NewsItem { get; } = newsItem;
}