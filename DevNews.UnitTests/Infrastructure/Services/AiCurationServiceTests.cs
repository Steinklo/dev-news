using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using DevNews.Domain.NewsItem.Enums;
using DevNews.Infrastructure.Services;
using NSubstitute;

namespace DevNews.UnitTests.Infrastructure.Services;

public class AiCurationServiceTests
{
    private readonly IAiService _aiService = Substitute.For<IAiService>();
    private readonly AiCurationService _sut;

    public AiCurationServiceTests()
    {
        _sut = new AiCurationService(_aiService);
    }

    private static CrawledArticle CreateCrawledArticle(string url = "https://openai.com/blog/new-model") =>
        new("<html>Article content here</html>", new Uri(url));

    private static string BuildSuccessJson(
        string title = "New GPT-5 Model Released with Major Improvements",
        string? summary = null,
        string category = "AiModelsAndApis",
        int relevanceScore = 85,
        string? severity = null,
        string? tags = null,
        string? publishedAt = null,
        string? author = null)
    {
        summary ??= Application.TestData.ValidSummary;
        var severityJson = severity != null ? $"\"{severity}\"" : "null";
        var tagsJson = tags ?? """["gpt-5", "openai", "llm"]""";
        var publishedAtJson = publishedAt != null ? $"\"{publishedAt}\"" : "null";
        var authorJson = author != null ? $"\"{author}\"" : "null";

        return $$"""
            {
                "isSuccess": true,
                "data": {
                    "title": "{{title}}",
                    "summary": "{{summary}}",
                    "category": "{{category}}",
                    "relevanceScore": {{relevanceScore}},
                    "severity": {{severityJson}},
                    "tags": {{tagsJson}},
                    "publishedAt": {{publishedAtJson}},
                    "author": {{authorJson}}
                },
                "errorMessage": null
            }
            """;
    }

    private void SetupAiResponse(string json)
    {
        _aiService.GenerateAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<string>.Success(json));
    }

    [Fact]
    public async Task CurateAsync_ValidResponse_ReturnsCleanedArticle()
    {
        SetupAiResponse(BuildSuccessJson());
        var crawled = CreateCrawledArticle();

        var result = await _sut.CurateAsync(crawled);

        Assert.True(result.IsSuccess);
        Assert.Equal("New GPT-5 Model Released with Major Improvements", result.Data!.Title);
        Assert.Equal(CategoryEnum.AiModelsAndApis, result.Data.Category);
        Assert.Equal(85, result.Data.RelevanceScore);
    }

    [Fact]
    public async Task CurateAsync_AiReturnsIsSuccessFalse_ReturnsFailure()
    {
        SetupAiResponse("""{"isSuccess": false, "data": null, "errorMessage": "Not relevant content"}""");
        var crawled = CreateCrawledArticle();

        var result = await _sut.CurateAsync(crawled);

        Assert.False(result.IsSuccess);
        Assert.Contains("Not relevant content", result.ErrorMessage);
    }

    [Fact]
    public async Task CurateAsync_AiServiceFails_ReturnsFailure()
    {
        _aiService.GenerateAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<string>.Failure("API error"));
        var crawled = CreateCrawledArticle();

        var result = await _sut.CurateAsync(crawled);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task CurateAsync_MissingTitle_ReturnsFailure()
    {
        SetupAiResponse("""
            {
                "isSuccess": true,
                "data": {
                    "summary": "Some summary",
                    "category": "AiModelsAndApis",
                    "relevanceScore": 85
                },
                "errorMessage": null
            }
            """);
        var crawled = CreateCrawledArticle();

        var result = await _sut.CurateAsync(crawled);

        Assert.False(result.IsSuccess);
        Assert.Contains("title", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CurateAsync_MissingSummary_ReturnsFailure()
    {
        SetupAiResponse("""
            {
                "isSuccess": true,
                "data": {
                    "title": "Some Title That Is Long Enough",
                    "category": "AiModelsAndApis",
                    "relevanceScore": 85
                },
                "errorMessage": null
            }
            """);
        var crawled = CreateCrawledArticle();

        var result = await _sut.CurateAsync(crawled);

        Assert.False(result.IsSuccess);
        Assert.Contains("summary", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CurateAsync_InvalidCategory_ReturnsFailure()
    {
        SetupAiResponse(BuildSuccessJson(category: "InvalidCategory"));
        var crawled = CreateCrawledArticle();

        var result = await _sut.CurateAsync(crawled);

        Assert.False(result.IsSuccess);
        Assert.Contains("category", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CurateAsync_RelevanceScoreOutOfRange_ReturnsFailure()
    {
        SetupAiResponse(BuildSuccessJson(relevanceScore: 150));
        var crawled = CreateCrawledArticle();

        var result = await _sut.CurateAsync(crawled);

        Assert.False(result.IsSuccess);
        Assert.Contains("relevanceScore", result.ErrorMessage);
    }

    [Fact]
    public async Task CurateAsync_SecurityCategoryWithoutSeverity_ReturnsFailure()
    {
        SetupAiResponse(BuildSuccessJson(category: "AiSafetyAndSecurity", severity: null));
        var crawled = CreateCrawledArticle();

        var result = await _sut.CurateAsync(crawled);

        Assert.False(result.IsSuccess);
        Assert.Contains("severity", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CurateAsync_SecurityCategoryWithSeverity_ReturnsSuccess()
    {
        SetupAiResponse(BuildSuccessJson(category: "AiSafetyAndSecurity", severity: "High"));
        var crawled = CreateCrawledArticle();

        var result = await _sut.CurateAsync(crawled);

        Assert.True(result.IsSuccess);
        Assert.Equal(SeverityEnum.High, result.Data!.Severity);
    }

    [Fact]
    public async Task CurateAsync_NonSecurityCategoryWithSeverity_SeverityNulled()
    {
        SetupAiResponse(BuildSuccessJson(category: "AiModelsAndApis", severity: "High"));
        var crawled = CreateCrawledArticle();

        var result = await _sut.CurateAsync(crawled);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.Severity);
    }

    [Fact]
    public async Task CurateAsync_TagsCappedAtFiveAndLowercased()
    {
        SetupAiResponse(BuildSuccessJson(
            tags: """["GPT", "OpenAI", "LLM", "API", "Release", "Extra"]"""));
        var crawled = CreateCrawledArticle();

        var result = await _sut.CurateAsync(crawled);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Data!.Tags!.Count);
        Assert.All(result.Data.Tags, tag => Assert.Equal(tag, tag.ToLowerInvariant()));
    }

    [Fact]
    public async Task CurateAsync_ValidPublishedAt_ParsedCorrectly()
    {
        SetupAiResponse(BuildSuccessJson(publishedAt: "2026-03-15T10:00:00Z"));
        var crawled = CreateCrawledArticle();

        var result = await _sut.CurateAsync(crawled);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data!.PublishedAt);
        Assert.Equal(2026, result.Data.PublishedAt!.Value.Year);
    }

    [Fact]
    public async Task CurateAsync_JsonWrappedInCodeFence_ParsedSuccessfully()
    {
        var json = "```json\n" + BuildSuccessJson() + "\n```";
        SetupAiResponse(json);
        var crawled = CreateCrawledArticle();

        var result = await _sut.CurateAsync(crawled);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("https://openai.com/blog/article", "OpenAI")]
    [InlineData("https://www.anthropic.com/news", "Anthropic")]
    [InlineData("https://blog.langchain.dev/post", "LangChain")]
    [InlineData("https://unknown-site.io/article", "Unknown-site")]
    public async Task CurateAsync_ResolvesSource_FromUrl(string url, string expectedSource)
    {
        SetupAiResponse(BuildSuccessJson());
        var crawled = CreateCrawledArticle(url);

        var result = await _sut.CurateAsync(crawled);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedSource, result.Data!.Source);
    }

    [Fact]
    public async Task CurateAsync_MalformedJson_ReturnsFailure()
    {
        SetupAiResponse("this is not json at all");
        var crawled = CreateCrawledArticle();

        var result = await _sut.CurateAsync(crawled);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task CurateAsync_SummaryExceedsMaxLength_ClampsInsteadOfDropping()
    {
        // A dense summary longer than the cap — what the stronger model can produce.
        var sentence = "This release ships a faster inference path and a cleaner agent API for developers. ";
        var longSummary = "";
        for (var i = 0; i < 20; i++)
            longSummary += sentence;
        Assert.True(longSummary.Length > CurationRules.MaxSummaryLength);

        SetupAiResponse(BuildSuccessJson(summary: longSummary));
        var crawled = CreateCrawledArticle();

        var result = await _sut.CurateAsync(crawled);

        // Previously this dropped the article ("Summary cannot exceed 1000 characters").
        Assert.True(result.IsSuccess);
        Assert.InRange(result.Data!.Summary.Length, CurationRules.MinSummaryLength, CurationRules.MaxSummaryLength);
    }
}
