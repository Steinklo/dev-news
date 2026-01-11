using DevNews.Domain.NewsItem.Enums;
using DevNews.Domain.NewsItem.Events;
using FluentAssertions;

namespace DevNews.UnitTests.Domain.NewsItem.Events;

public class NewsUpdatedEventTests
{
    private static DevNews.Domain.NewsItem.NewsItem CreateValidNewsItem()
    {
        return DevNews.Domain.NewsItem.NewsItem.Create(
            title: "Test News Title",
            summary: "This is a test summary that meets the minimum length requirement.",
            url: "https://example.com/article",
            category: CategoryEnum.CloudAndInfrastructure,
            relevanceScore: 65).Data!;
    }

    [Fact]
    public void Constructor_SetsNewsItem()
    {
        var newsItem = CreateValidNewsItem();

        var evt = new NewsUpdatedEvent(newsItem);

        evt.NewsItem.Should().Be(newsItem);
    }

    [Fact]
    public void Constructor_SetsAggregateId()
    {
        var newsItem = CreateValidNewsItem();

        var evt = new NewsUpdatedEvent(newsItem);

        evt.AggregateId.Should().Be(newsItem.Id);
    }

    [Fact]
    public void Constructor_GeneratesUniqueEventId()
    {
        var newsItem = CreateValidNewsItem();

        var evt1 = new NewsUpdatedEvent(newsItem);
        var evt2 = new NewsUpdatedEvent(newsItem);

        evt1.Id.Should().NotBe(evt2.Id);
        evt1.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Constructor_SetsCreatedAtToNow()
    {
        var before = DateTime.UtcNow;
        var newsItem = CreateValidNewsItem();

        var evt = new NewsUpdatedEvent(newsItem);

        evt.CreatedAt.Should().BeOnOrAfter(before);
        evt.CreatedAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public void Constructor_InheritsDomainEventBaseTimestamp()
    {
        var newsItem = CreateValidNewsItem();

        var evt = new NewsUpdatedEvent(newsItem);

        evt.Created.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}
