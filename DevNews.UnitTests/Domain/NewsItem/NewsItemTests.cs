using DevNews.Domain.NewsItem.Enums;
using DevNews.Domain.NewsItem.Events;
using FluentAssertions;

namespace DevNews.UnitTests.Domain.NewsItem;

public class NewsItemTests
{
    private const string ValidTitle = "Critical Security Vulnerability in OpenSSL";
    private const string ValidSummary = "A critical security vulnerability has been discovered in OpenSSL that allows remote code execution.";
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

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
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
        newsItem.Title.Value.Should().Be(ValidTitle);
        newsItem.Summary.Value.Should().Be(ValidSummary);
        newsItem.Url.Value.Should().Be(ValidUrl);
        newsItem.Category.Value.Should().Be(ValidCategory);
        newsItem.RelevanceScore.Value.Should().Be(ValidRelevanceScore);
        newsItem.PublishedAt.Should().Be(publishedAt);
        newsItem.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        newsItem.UpdatedAt.Should().BeNull();
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

        result.Data!.Id.Should().NotBe(Guid.Empty);
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
        newsItem.DomainEvents.Should().HaveCount(1);
        newsItem.DomainEvents.First().Should().BeOfType<NewsCreatedEvent>();
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

        newsItem.DomainEvents.Should().BeEmpty();
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

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Title");
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    public void Create_WithInvalidSummary_ReturnsFailure(string summary)
    {
        var result = DevNews.Domain.NewsItem.NewsItem.Create(
            title: ValidTitle,
            summary: summary,
            url: ValidUrl,
            category: ValidCategory,
            relevanceScore: ValidRelevanceScore);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Summary");
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

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("URL");
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

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Relevance");
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

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("category");
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

        result.IsSuccess.Should().BeTrue();
        result.Data!.Severity.Should().Be(SeverityEnum.Critical);
    }

    [Theory]
    [InlineData(CategoryEnum.ProgrammingLanguagesAndRuntimes)]
    [InlineData(CategoryEnum.FrameworksAndLibraries)]
    [InlineData(CategoryEnum.CloudAndInfrastructure)]
    [InlineData(CategoryEnum.DevOpsCiCdObservabilityTesting)]
    [InlineData(CategoryEnum.AiMlDeveloperTooling)]
    [InlineData(CategoryEnum.PerformanceAndArchitecturePatterns)]
    [InlineData(CategoryEnum.DeveloperToolsIdesProductivity)]
    public void Create_WithSeverityForNonSecurityCategory_ReturnsFailure(CategoryEnum category)
    {
        var result = DevNews.Domain.NewsItem.NewsItem.Create(
            title: ValidTitle,
            summary: ValidSummary,
            url: ValidUrl,
            category: category,
            relevanceScore: ValidRelevanceScore,
            severity: SeverityEnum.High);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Severity can only be set for SecurityAndVulnerabilities");
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

        result.Data!.Tags.Should().BeEquivalentTo(tags);
    }

    [Fact]
    public void Create_WithMoreThan5Tags_OnlyStoresFirst5()
    {
        var tags = new[] { "tag1", "tag2", "tag3", "tag4", "tag5", "tag6", "tag7" };

        var result = DevNews.Domain.NewsItem.NewsItem.Create(
            title: ValidTitle,
            summary: ValidSummary,
            url: ValidUrl,
            category: ValidCategory,
            relevanceScore: ValidRelevanceScore,
            tags: tags);

        result.Data!.Tags.Should().HaveCount(5);
        result.Data!.Tags.Should().BeEquivalentTo(tags.Take(5));
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

        result.Data!.Tags.Should().BeEmpty();
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

        result.IsSuccess.Should().BeTrue();
        result.Data!.Severity.Should().BeNull();
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

        result.IsSuccess.Should().BeTrue();
        result.Data!.Severity.Should().Be(severity);
    }
}
