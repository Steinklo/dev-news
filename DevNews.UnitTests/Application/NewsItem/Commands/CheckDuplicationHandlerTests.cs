using DevNews.Application.Common.Models;
using DevNews.Application.Common.Services;
using DevNews.Application.NewsItem.Commands;
using DevNews.Domain.Common;
using DevNews.Domain.NewsItem.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DevNews.UnitTests.Application.NewsItem.Commands;

public class CheckDuplicationHandlerTests
{
    private readonly IDuplicationService _duplicationService = Substitute.For<IDuplicationService>();
    private readonly CheckDuplicationHandler _handler;

    private static CleanedArticle CreateArticle() => new(
        Title: "Critical Security Vulnerability Found in Popular Library",
        Summary: TestData.ValidSummary,
        Category: CategoryEnum.AiModelsAndApis,
        Url: new Uri("https://example.com/article"),
        RelevanceScore: 85,
        PublishedAt: DateTimeOffset.UtcNow);

    public CheckDuplicationHandlerTests()
    {
        _handler = new CheckDuplicationHandler(
            _duplicationService,
            NullLogger<CheckDuplicationHandler>.Instance);
    }

    [Fact]
    public async Task Handle_DuplicateFound_ReturnsTrue()
    {
        var article = CreateArticle();
        _duplicationService.IsDuplicateAsync(article, Arg.Any<CancellationToken>())
            .Returns(ResultResponse<bool>.Success(true));

        var result = await _handler.Handle(new CheckDuplicationCommand(article), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
    }

    [Fact]
    public async Task Handle_NotDuplicate_ReturnsFalse()
    {
        var article = CreateArticle();
        _duplicationService.IsDuplicateAsync(article, Arg.Any<CancellationToken>())
            .Returns(ResultResponse<bool>.Success(false));

        var result = await _handler.Handle(new CheckDuplicationCommand(article), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Data);
    }

    [Fact]
    public async Task Handle_ServiceFails_ReturnsFailure()
    {
        var article = CreateArticle();
        _duplicationService.IsDuplicateAsync(article, Arg.Any<CancellationToken>())
            .Returns(ResultResponse<bool>.Failure("Duplication check failed"));

        var result = await _handler.Handle(new CheckDuplicationCommand(article), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Duplication check failed", result.ErrorMessage);
    }
}
