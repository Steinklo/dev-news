using DevNews.Application.Common.Repositories;
using DevNews.Application.NewsItem.Queries;
using DevNews.Domain.Common;
using NSubstitute;

namespace DevNews.UnitTests.Application.NewsItem.Queries;

public class GetNewsByIdHandlerTests
{
    private readonly INewsItemRepository _repository = Substitute.For<INewsItemRepository>();
    private readonly GetNewsByIdHandler _handler;

    public GetNewsByIdHandlerTests()
    {
        _handler = new GetNewsByIdHandler(_repository);
    }

    [Fact]
    public async Task Handle_ItemFound_ReturnsDto()
    {
        var newsItem = TestData.CreateValidNewsItem();
        _repository.GetByIdAsync(newsItem.Id, Arg.Any<CancellationToken>())
            .Returns(ResultResponse<DevNews.Domain.NewsItem.NewsItem?>.Success(newsItem));

        var result = await _handler.Handle(new GetNewsByIdQuery(newsItem.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(newsItem.Title.Value, result.Data!.Title);
    }

    [Fact]
    public async Task Handle_ItemNotFound_ReturnsSuccessWithNull()
    {
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(ResultResponse<DevNews.Domain.NewsItem.NewsItem?>.Success(null));

        var result = await _handler.Handle(new GetNewsByIdQuery(id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data);
    }
}
