using DevNews.Domain.NewsItem.Enums;
using DevNews.Domain.NewsItem.Events;

namespace DevNews.UnitTests.Domain.NewsItem.Events;

public class NewsCreatedEventTests
{
    // 80+ words (~400 chars) per CLAUDE.md spec for TL;DR summaries
    private const string ValidSummary = "This comprehensive security advisory details a critical remote code execution vulnerability " +
                                         "discovered in the widely-used OpenSSL cryptographic library. The flaw, identified as CVE-2026-1234, " +
                                         "affects versions 3.0 through 3.2.1 and allows unauthenticated attackers to execute arbitrary code " +
                                         "on vulnerable systems. Organizations running affected versions should immediately upgrade to the " +
                                         "patched release 3.2.2. The vulnerability was responsibly disclosed by security researchers and " +
                                         "has been assigned a CVSS score of 9.8 indicating critical severity.";

    private static DevNews.Domain.NewsItem.NewsItem CreateValidNewsItem()
    {
        return DevNews.Domain.NewsItem.NewsItem.Create(
            title: "Critical Security Vulnerability Alert",
            summary: ValidSummary,
            url: "https://example.com/article",
            category: CategoryEnum.AiSafetyAndSecurity,
            relevanceScore: 75).Data!;
    }

    [Fact]
    public void Constructor_SetsNewsItem()
    {
        var newsItem = CreateValidNewsItem();
        newsItem.ClearDomainEvents();

        var evt = new NewsCreatedEvent(newsItem);

        Assert.Equal(newsItem, evt.NewsItem);
    }

    [Fact]
    public void Constructor_SetsAggregateId()
    {
        var newsItem = CreateValidNewsItem();
        newsItem.ClearDomainEvents();

        var evt = new NewsCreatedEvent(newsItem);

        Assert.Equal(newsItem.Id, evt.AggregateId);
    }

    [Fact]
    public void Constructor_GeneratesUniqueEventId()
    {
        var newsItem = CreateValidNewsItem();
        newsItem.ClearDomainEvents();

        var evt1 = new NewsCreatedEvent(newsItem);
        var evt2 = new NewsCreatedEvent(newsItem);

        Assert.NotEqual(evt1.Id, evt2.Id);
        Assert.NotEqual(Guid.Empty, evt1.Id);
    }

    [Fact]
    public void Constructor_SetsCreatedAtToNow()
    {
        var before = DateTime.UtcNow;
        var newsItem = CreateValidNewsItem();
        newsItem.ClearDomainEvents();

        var evt = new NewsCreatedEvent(newsItem);

        Assert.True(evt.CreatedAt >= before);
        Assert.True(evt.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void NewsItemCreate_RaisesNewsCreatedEvent()
    {
        var newsItem = DevNews.Domain.NewsItem.NewsItem.Create(
            title: "Critical Security Vulnerability Alert",
            summary: ValidSummary,
            url: "https://example.com/article",
            category: CategoryEnum.AgentsAndFrameworks,
            relevanceScore: 80).Data!;

        var evt = Assert.Single(newsItem.DomainEvents);
        var createdEvent = Assert.IsType<NewsCreatedEvent>(evt);
        Assert.Equal(newsItem, createdEvent.NewsItem);
    }
}
