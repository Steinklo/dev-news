using DevNews.Domain.NewsItem.Enums;
using DevNews.Domain.NewsItem.Events;

namespace DevNews.UnitTests.Domain.NewsItem;

public class NewsItemTests
{
    private const string ValidTitle = "Critical Security Vulnerability in OpenSSL";
    // 80+ words (~400 chars) per CLAUDE.md spec for TL;DR summaries
    private const string ValidSummary = "This comprehensive security advisory details a critical remote code execution vulnerability " +
                                         "discovered in the widely-used OpenSSL cryptographic library. The flaw, identified as CVE-2026-1234, " +
                                         "affects versions 3.0 through 3.2.1 and allows unauthenticated attackers to execute arbitrary code " +
                                         "on vulnerable systems. Organizations running affected versions should immediately upgrade to the " +
                                         "patched release 3.2.2. The vulnerability was responsibly disclosed by security researchers and " +
                                         "has been assigned a CVSS score of 9.8 indicating critical severity.";
    private const string ValidUrl = "https://example.com/security-advisory";
    private const int ValidRelevanceScore = 85;
    private static readonly CategoryEnum ValidCategory = CategoryEnum.SecurityAndVulnerabilities;

    [Fact]
    public void Create_WithValidData_ReturnsSuccess()
    {
        var result = DevNews.Domain.NewsItem.NewsItem.Create(
            title: ValidTitle,
            summary: ValidSummary,
            url: ValidUrl,
            category: ValidCategory,
            relevanceScore: ValidRelevanceScore);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public void Create_WithValidData_SetsPropertiesCorrectly()
    {
        var publishedAt = DateTimeOffset.UtcNow.AddHours(-1);

        var result = DevNews.Domain.NewsItem.NewsItem.Create(
            title: ValidTitle,
            summary: ValidSummary,
            url: ValidUrl,
            category: ValidCategory,
            relevanceScore: ValidRelevanceScore,
            publishedAt: publishedAt);

        var newsItem = result.Data!;
        Assert.Equal(ValidTitle, newsItem.Title.Value);
        Assert.Equal(ValidSummary, newsItem.Summary.Value);
        Assert.Equal(ValidUrl, newsItem.Url.Value);
        Assert.Equal(ValidCategory, newsItem.Category.Value);
        Assert.Equal(ValidRelevanceScore, newsItem.RelevanceScore.Value);
        Assert.Equal(publishedAt, newsItem.PublishedAt);
        Assert.True(Math.Abs((newsItem.CreatedAt - DateTimeOffset.UtcNow).TotalSeconds) < 1);
        Assert.Null(newsItem.UpdatedAt);
    }

    [Fact]
    public void Create_GeneratesGuidV7Id()
    {
        var result = DevNews.Domain.NewsItem.NewsItem.Create(
            title: ValidTitle,
            summary: ValidSummary,
            url: ValidUrl,
            category: ValidCategory,
            relevanceScore: ValidRelevanceScore);

        Assert.NotEqual(Guid.Empty, result.Data!.Id);
    }

    [Fact]
    public void Create_RaisesNewsCreatedEvent()
    {
        var result = DevNews.Domain.NewsItem.NewsItem.Create(
            title: ValidTitle,
            summary: ValidSummary,
            url: ValidUrl,
            category: ValidCategory,
            relevanceScore: ValidRelevanceScore);

        var newsItem = result.Data!;
        Assert.Single(newsItem.DomainEvents);
        Assert.IsType<NewsCreatedEvent>(newsItem.DomainEvents.First());
    }

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var result = DevNews.Domain.NewsItem.NewsItem.Create(
            title: ValidTitle,
            summary: ValidSummary,
            url: ValidUrl,
            category: ValidCategory,
            relevanceScore: ValidRelevanceScore);

        var newsItem = result.Data!;
        newsItem.ClearDomainEvents();

        Assert.Empty(newsItem.DomainEvents);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidTitle_ReturnsFailure(string title)
    {
        var result = DevNews.Domain.NewsItem.NewsItem.Create(
            title: title,
            summary: ValidSummary,
            url: ValidUrl,
            category: ValidCategory,
            relevanceScore: ValidRelevanceScore);

        Assert.False(result.IsSuccess);
        Assert.Contains("Title", result.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("This summary is too short to meet the 80-word minimum requirement per CLAUDE.md specification.")]
    public void Create_WithInvalidSummary_ReturnsFailure(string summary)
    {
        var result = DevNews.Domain.NewsItem.NewsItem.Create(
            title: ValidTitle,
            summary: summary,
            url: ValidUrl,
            category: ValidCategory,
            relevanceScore: ValidRelevanceScore);

        Assert.False(result.IsSuccess);
        Assert.Contains("Summary", result.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("ftp://invalid.com")]
    public void Create_WithInvalidUrl_ReturnsFailure(string url)
    {
        var result = DevNews.Domain.NewsItem.NewsItem.Create(
            title: ValidTitle,
            summary: ValidSummary,
            url: url,
            category: ValidCategory,
            relevanceScore: ValidRelevanceScore);

        Assert.False(result.IsSuccess);
        Assert.Contains("URL", result.ErrorMessage);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_WithInvalidRelevanceScore_ReturnsFailure(int score)
    {
        var result = DevNews.Domain.NewsItem.NewsItem.Create(
            title: ValidTitle,
            summary: ValidSummary,
            url: ValidUrl,
            category: ValidCategory,
            relevanceScore: score);

        Assert.False(result.IsSuccess);
        Assert.Contains("Relevance", result.ErrorMessage);
    }

    [Fact]
    public void Create_WithInvalidCategory_ReturnsFailure()
    {
        var invalidCategory = (CategoryEnum)999;

        var result = DevNews.Domain.NewsItem.NewsItem.Create(
            title: ValidTitle,
            summary: ValidSummary,
            url: ValidUrl,
            category: invalidCategory,
            relevanceScore: ValidRelevanceScore);

        Assert.False(result.IsSuccess);
        Assert.Contains("category", result.ErrorMessage);
    }

    [Fact]
    public void Create_WithSeverityForSecurityCategory_ReturnsSuccess()
    {
        var result = DevNews.Domain.NewsItem.NewsItem.Create(
            title: ValidTitle,
            summary: ValidSummary,
            url: ValidUrl,
            category: CategoryEnum.SecurityAndVulnerabilities,
            relevanceScore: ValidRelevanceScore,
            severity: SeverityEnum.Critical);

        Assert.True(result.IsSuccess);
        Assert.Equal(SeverityEnum.Critical, result.Data!.Severity);
    }

    [Theory]
    [InlineData(CategoryEnum.AiModelsAndApis)]
    [InlineData(CategoryEnum.AiDeveloperTools)]
    [InlineData(CategoryEnum.AgentsAndFrameworks)]
    [InlineData(CategoryEnum.AiInfrastructure)]
    [InlineData(CategoryEnum.CloudAndInfrastructure)]
    [InlineData(CategoryEnum.OpenSourceAndCommunity)]
    public void Create_WithSeverityForNonSecurityCategory_ReturnsFailure(CategoryEnum category)
    {
        var result = DevNews.Domain.NewsItem.NewsItem.Create(
            title: ValidTitle,
            summary: ValidSummary,
            url: ValidUrl,
            category: category,
            relevanceScore: ValidRelevanceScore,
            severity: SeverityEnum.High);

        Assert.False(result.IsSuccess);
        Assert.Contains("Severity can only be set for SecurityAndVulnerabilities", result.ErrorMessage);
    }

    [Fact]
    public void Create_WithTags_StoresTags()
    {
        var tags = new[] { "cve", "critical", "openssl" };

        var result = DevNews.Domain.NewsItem.NewsItem.Create(
            title: ValidTitle,
            summary: ValidSummary,
            url: ValidUrl,
            category: ValidCategory,
            relevanceScore: ValidRelevanceScore,
            tags: tags);

        Assert.Equal(tags.Length, result.Data!.Tags.Count);
        foreach (var tag in tags)
        {
            Assert.Contains(tag, result.Data!.Tags);
        }
    }

    [Fact]
    public void Create_WithManyTags_StoresAllTags()
    {
        var tags = new[] { "tag1", "tag2", "tag3", "tag4", "tag5", "tag6", "tag7" };

        var result = DevNews.Domain.NewsItem.NewsItem.Create(
            title: ValidTitle,
            summary: ValidSummary,
            url: ValidUrl,
            category: ValidCategory,
            relevanceScore: ValidRelevanceScore,
            tags: tags);

        Assert.Equal(7, result.Data!.Tags.Count);
        foreach (var tag in tags)
        {
            Assert.Contains(tag, result.Data!.Tags);
        }
    }

    [Fact]
    public void Create_WithNullTags_CreatesEmptyTagList()
    {
        var result = DevNews.Domain.NewsItem.NewsItem.Create(
            title: ValidTitle,
            summary: ValidSummary,
            url: ValidUrl,
            category: ValidCategory,
            relevanceScore: ValidRelevanceScore,
            tags: null);

        Assert.Empty(result.Data!.Tags);
    }

    [Fact]
    public void Create_WithoutSeverityForSecurityCategory_AllowsNull()
    {
        var result = DevNews.Domain.NewsItem.NewsItem.Create(
            title: ValidTitle,
            summary: ValidSummary,
            url: ValidUrl,
            category: CategoryEnum.SecurityAndVulnerabilities,
            relevanceScore: ValidRelevanceScore,
            severity: null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.Severity);
    }

    [Theory]
    [InlineData(SeverityEnum.Low)]
    [InlineData(SeverityEnum.Medium)]
    [InlineData(SeverityEnum.High)]
    [InlineData(SeverityEnum.Critical)]
    public void Create_AllSeverityLevels_AreValid(SeverityEnum severity)
    {
        var result = DevNews.Domain.NewsItem.NewsItem.Create(
            title: ValidTitle,
            summary: ValidSummary,
            url: ValidUrl,
            category: CategoryEnum.SecurityAndVulnerabilities,
            relevanceScore: ValidRelevanceScore,
            severity: severity);

        Assert.True(result.IsSuccess);
        Assert.Equal(severity, result.Data!.Severity);
    }
}
