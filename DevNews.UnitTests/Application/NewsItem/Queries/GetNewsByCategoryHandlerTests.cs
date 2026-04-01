using DevNews.Application.Common.Repositories;
using DevNews.Application.NewsItem.Queries;
using DevNews.Domain.Common;
using DevNews.Domain.NewsItem.Enums;
using NSubstitute;

namespace DevNews.UnitTests.Application.NewsItem.Queries;

public class GetNewsByCategoryHandlerTests
{
    private readonly INewsItemRepository _repository = Substitute.For<INewsItemRepository>();
    private readonly GetNewsByCategoryHandler _handler;

    public GetNewsByCategoryHandlerTests()
    {
        _handler = new GetNewsByCategoryHandler(_repository);
    }

    [Fact]
    public async Task Handle_WithItems_ReturnsSuccessWithMappedDtos()
    {
        var newsItem = TestData.CreateValidNewsItem();
        var items = new List<DevNews.Domain.NewsItem.NewsItem> { newsItem };
        var start = DateTimeOffset.UtcNow.AddDays(-30);
        var end = DateTimeOffset.UtcNow;

        _repository.GetByCategoryAndMonthAsync(
                CategoryEnum.AiModelsAndApis, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<IEnumerable<DevNews.Domain.NewsItem.NewsItem>>.Success(items));

        var query = new GetNewsByCategoryQuery(CategoryEnum.AiModelsAndApis, start, end);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.Count);
        Assert.Equal("AiModelsAndApis", result.Data.Category);
    }

    [Fact]
    public async Task Handle_EmptyResult_ReturnsSuccessWithZeroCount()
    {
        var start = DateTimeOffset.UtcNow.AddDays(-30);
        var end = DateTimeOffset.UtcNow;

        _repository.GetByCategoryAndMonthAsync(
                Arg.Any<CategoryEnum>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<IEnumerable<DevNews.Domain.NewsItem.NewsItem>>.Success(
                Enumerable.Empty<DevNews.Domain.NewsItem.NewsItem>()));

        var query = new GetNewsByCategoryQuery(CategoryEnum.AiDeveloperTools, start, end);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Data!.Count);
        Assert.Empty(result.Data.Items);
    }

    [Fact]
    public async Task Handle_RepositoryFails_ReturnsFailure()
    {
        var start = DateTimeOffset.UtcNow.AddDays(-30);
        var end = DateTimeOffset.UtcNow;

        _repository.GetByCategoryAndMonthAsync(
                Arg.Any<CategoryEnum>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<IEnumerable<DevNews.Domain.NewsItem.NewsItem>>.Failure("Query failed"));

        var query = new GetNewsByCategoryQuery(CategoryEnum.AiModelsAndApis, start, end);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Query failed", result.ErrorMessage);
    }
}
