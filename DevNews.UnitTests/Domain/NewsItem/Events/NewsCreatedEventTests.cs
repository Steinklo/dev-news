using DevNews.Domain.NewsItem.Enums;
using DevNews.Domain.NewsItem.Events;
using FluentAssertions;

namespace DevNews.UnitTests.Domain.NewsItem.Events;

public class NewsCreatedEventTests
{
    private static DevNews.Domain.NewsItem.NewsItem CreateValidNewsItem()
    {
        return DevNews.Domain.NewsItem.NewsItem.Create(
            title: "Test News Title",
            summary: "This is a test summary that meets the minimum length requirement.",
            url: "https://example.com/article",
            category: CategoryEnum.SecurityAndVulnerabilities,
            relevanceScore: 75).Data!;
    }

    [Fact]
    public void Constructor_SetsNewsItem()
    {
        var newsItem = CreateValidNewsItem();
        newsItem.ClearDomainEvents();

        var evt = new NewsCreatedEvent(newsItem);

        evt.NewsItem.Should().Be(newsItem);
    }

    [Fact]
    public void Constructor_SetsAggregateId()
    {
        var newsItem = CreateValidNewsItem();
        newsItem.ClearDomainEvents();

        var evt = new NewsCreatedEvent(newsItem);

        evt.AggregateId.Should().Be(newsItem.Id);
    }

    [Fact]
    public void Constructor_GeneratesUniqueEventId()
    {
        var newsItem = CreateValidNewsItem();
        newsItem.ClearDomainEvents();

        var evt1 = new NewsCreatedEvent(newsItem);
        var evt2 = new NewsCreatedEvent(newsItem);

        evt1.Id.Should().NotBe(evt2.Id);
        evt1.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Constructor_SetsCreatedAtToNow()
    {
        var before = DateTime.UtcNow;
        var newsItem = CreateValidNewsItem();
        newsItem.ClearDomainEvents();

        var evt = new NewsCreatedEvent(newsItem);

        evt.CreatedAt.Should().BeOnOrAfter(before);
        evt.CreatedAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public void NewsItemCreate_RaisesNewsCreatedEvent()
    {
        var newsItem = DevNews.Domain.NewsItem.NewsItem.Create(
            title: "Test News Title",
            summary: "This is a test summary that meets the minimum length requirement.",
            url: "https://example.com/article",
            category: CategoryEnum.FrameworksAndLibraries,
            relevanceScore: 80).Data!;

        newsItem.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<NewsCreatedEvent>()
            .Which.NewsItem.Should().Be(newsItem);
    }
}
