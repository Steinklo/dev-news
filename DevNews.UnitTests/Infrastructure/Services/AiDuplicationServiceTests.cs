using DevNews.Application.Common.Models;
using DevNews.Application.Common.Repositories;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using DevNews.Domain.NewsItem.Enums;
using DevNews.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DevNews.UnitTests.Infrastructure.Services;

public class AiDuplicationServiceTests
{
    private readonly IAiService _aiService = Substitute.For<IAiService>();
    private readonly INewsItemRepository _repository = Substitute.For<INewsItemRepository>();
    private readonly AiDuplicationService _sut;

    public AiDuplicationServiceTests()
    {
        _sut = new AiDuplicationService(
            _aiService,
            _repository,
            NullLogger<AiDuplicationService>.Instance);
    }

    private static CleanedArticle CreateArticle(DateTimeOffset? publishedAt = null) =>
        new(
            Title: Application.TestData.ValidTitle,
            Summary: Application.TestData.ValidSummary,
            Category: CategoryEnum.AiModelsAndApis,
            Url: new Uri("https://example.com/new-article"),
            RelevanceScore: 85,
            PublishedAt: publishedAt);

    private void SetupRepositoryWithArticles(int count = 1)
    {
        var articles = Enumerable.Range(0, count)
            .Select(_ => Application.TestData.CreateValidNewsItem())
            .ToList();

        _repository.GetByCategoryAndMonthAsync(
                Arg.Any<CategoryEnum>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(ResultResponse<IEnumerable<DevNews.Domain.NewsItem.NewsItem>>.Success(articles));
    }

    private void SetupRepositoryEmpty()
    {
        _repository.GetByCategoryAndMonthAsync(
                Arg.Any<CategoryEnum>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(ResultResponse<IEnumerable<DevNews.Domain.NewsItem.NewsItem>>.Success(
                Enumerable.Empty<DevNews.Domain.NewsItem.NewsItem>()));
    }

    private void SetupAiResponse(string json)
    {
        _aiService.GenerateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<string>.Success(json));
    }

    [Fact]
    public async Task IsDuplicateAsync_NoSameMonthArticles_ReturnsFalseWithoutAiCall()
    {
        SetupRepositoryEmpty();
        var article = CreateArticle(DateTimeOffset.UtcNow);

        var result = await _sut.IsDuplicateAsync(article);

        Assert.True(result.IsSuccess);
        Assert.False(result.Data);
        await _aiService.DidNotReceive().GenerateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IsDuplicateAsync_AiReturnsDuplicate_ReturnsTrue()
    {
        SetupRepositoryWithArticles();
        SetupAiResponse("""{"isDuplicate": true, "reason": "Same announcement"}""");
        var article = CreateArticle(DateTimeOffset.UtcNow);

        var result = await _sut.IsDuplicateAsync(article);

        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
    }

    [Fact]
    public async Task IsDuplicateAsync_AiReturnsNotDuplicate_ReturnsFalse()
    {
        SetupRepositoryWithArticles();
        SetupAiResponse("""{"isDuplicate": false, "reason": "Different topics"}""");
        var article = CreateArticle(DateTimeOffset.UtcNow);

        var result = await _sut.IsDuplicateAsync(article);

        Assert.True(result.IsSuccess);
        Assert.False(result.Data);
    }

    [Fact]
    public async Task IsDuplicateAsync_AiServiceFails_FailsOpenReturnsFalse()
    {
        SetupRepositoryWithArticles();
        _aiService.GenerateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<string>.Failure("API error"));
        var article = CreateArticle(DateTimeOffset.UtcNow);

        var result = await _sut.IsDuplicateAsync(article);

        Assert.True(result.IsSuccess);
        Assert.False(result.Data);
    }

    [Fact]
    public async Task IsDuplicateAsync_MalformedAiResponse_FailsOpenReturnsFalse()
    {
        SetupRepositoryWithArticles();
        SetupAiResponse("not valid json");
        var article = CreateArticle(DateTimeOffset.UtcNow);

        var result = await _sut.IsDuplicateAsync(article);

        Assert.True(result.IsSuccess);
        Assert.False(result.Data);
    }

    [Fact]
    public async Task IsDuplicateAsync_MissingIsDuplicateField_FailsOpenReturnsFalse()
    {
        SetupRepositoryWithArticles();
        SetupAiResponse("""{"reason": "Some reason but missing isDuplicate"}""");
        var article = CreateArticle(DateTimeOffset.UtcNow);

        var result = await _sut.IsDuplicateAsync(article);

        Assert.True(result.IsSuccess);
        Assert.False(result.Data);
    }

    [Fact]
    public async Task IsDuplicateAsync_NullPublishedAt_UsesUtcNow()
    {
        SetupRepositoryEmpty();
        var article = CreateArticle(publishedAt: null);

        var result = await _sut.IsDuplicateAsync(article);

        Assert.True(result.IsSuccess);
        // Verify repository was called with current month boundaries
        await _repository.Received(1).GetByCategoryAndMonthAsync(
            CategoryEnum.AiModelsAndApis,
            Arg.Is<DateTimeOffset>(d => d.Day == 1 && d.Month == DateTimeOffset.UtcNow.Month),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IsDuplicateAsync_RepositoryFails_ReturnsFalse()
    {
        _repository.GetByCategoryAndMonthAsync(
                Arg.Any<CategoryEnum>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(ResultResponse<IEnumerable<DevNews.Domain.NewsItem.NewsItem>>.Failure("DB error"));
        var article = CreateArticle(DateTimeOffset.UtcNow);

        var result = await _sut.IsDuplicateAsync(article);

        Assert.True(result.IsSuccess);
        Assert.False(result.Data);
    }
}
