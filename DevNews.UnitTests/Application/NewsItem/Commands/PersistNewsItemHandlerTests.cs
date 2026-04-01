using DevNews.Application.Common.Models;
using DevNews.Application.Common.Repositories;
using DevNews.Application.NewsItem.Commands;
using DevNews.Domain.Common;
using DevNews.Domain.NewsItem.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DevNews.UnitTests.Application.NewsItem.Commands;

public class PersistNewsItemHandlerTests
{
    private readonly INewsItemRepository _repository = Substitute.For<INewsItemRepository>();
    private readonly PersistNewsItemHandler _handler;

    private static CleanedArticle CreateArticle() => new(
        Title: "Critical Security Vulnerability Found in Popular Library",
        Summary: TestData.ValidSummary,
        Category: CategoryEnum.AiModelsAndApis,
        Url: new Uri("https://example.com/article"),
        RelevanceScore: 85,
        PublishedAt: DateTimeOffset.UtcNow);

    public PersistNewsItemHandlerTests()
    {
        _handler = new PersistNewsItemHandler(
            _repository,
            NullLogger<PersistNewsItemHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ValidArticle_ReturnsGuid()
    {
        var article = CreateArticle();
        _repository.AddAsync(Arg.Any<DevNews.Domain.NewsItem.NewsItem>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var item = callInfo.ArgAt<DevNews.Domain.NewsItem.NewsItem>(0);
                return ResultResponse<DevNews.Domain.NewsItem.NewsItem>.Success(item);
            });

        var result = await _handler.Handle(new PersistNewsItemCommand(article), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Data);
    }

    [Fact]
    public async Task Handle_RepositoryFails_ReturnsFailure()
    {
        var article = CreateArticle();
        _repository.AddAsync(Arg.Any<DevNews.Domain.NewsItem.NewsItem>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<DevNews.Domain.NewsItem.NewsItem>.Failure("Cosmos DB error"));

        var result = await _handler.Handle(new PersistNewsItemCommand(article), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Cosmos DB error", result.ErrorMessage);
    }
}
