using DevNews.Application.Common.Repositories;
using DevNews.Application.ShortVideo.Queries;
using DevNews.Domain.Common;
using DevNews.Domain.NewsItem.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DevNews.UnitTests.Application.ShortVideo.Queries;

public class SelectVideoEligibleItemsHandlerTests
{
    private readonly INewsItemRepository _newsItemRepository = Substitute.For<INewsItemRepository>();
    private readonly IShortVideoRepository _shortVideoRepository = Substitute.For<IShortVideoRepository>();
    private readonly SelectVideoEligibleItemsHandler _handler;

    public SelectVideoEligibleItemsHandlerTests()
    {
        _handler = new SelectVideoEligibleItemsHandler(
            _newsItemRepository,
            _shortVideoRepository,
            NullLogger<SelectVideoEligibleItemsHandler>.Instance);

        // Default: no existing videos
        _shortVideoRepository.GetNewsItemIdsWithVideosAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<IEnumerable<Guid>>.Success(Enumerable.Empty<Guid>()));

        // Default: empty results for all categories
        _newsItemRepository.GetByCategoryAndMonthAsync(
                Arg.Any<CategoryEnum>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<IEnumerable<DevNews.Domain.NewsItem.NewsItem>>.Success(
                Enumerable.Empty<DevNews.Domain.NewsItem.NewsItem>()));
    }

    [Fact]
    public async Task Handle_EligibleItems_ReturnsSortedByRelevance()
    {
        var item90 = TestData.CreateValidNewsItem(CategoryEnum.AiModelsAndApis, relevanceScore: 90);
        var item95 = TestData.CreateValidNewsItem(CategoryEnum.AiModelsAndApis, relevanceScore: 95);

        _newsItemRepository.GetByCategoryAndMonthAsync(
                CategoryEnum.AiModelsAndApis, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<IEnumerable<DevNews.Domain.NewsItem.NewsItem>>.Success(
                new[] { item90, item95 }));

        var query = new SelectVideoEligibleItemsQuery(MinRelevanceScore: 85, MaxItems: 5);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Count);
        Assert.Equal(95, result.Data[0].RelevanceScore);
        Assert.Equal(90, result.Data[1].RelevanceScore);
    }

    [Fact]
    public async Task Handle_ItemsWithExistingVideos_ExcludesThose()
    {
        var item1 = TestData.CreateValidNewsItem(CategoryEnum.AiModelsAndApis, relevanceScore: 90);
        var item2 = TestData.CreateValidNewsItem(CategoryEnum.AiModelsAndApis, relevanceScore: 95);

        _newsItemRepository.GetByCategoryAndMonthAsync(
                CategoryEnum.AiModelsAndApis, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<IEnumerable<DevNews.Domain.NewsItem.NewsItem>>.Success(
                new[] { item1, item2 }));

        // item1 already has a video
        _shortVideoRepository.GetNewsItemIdsWithVideosAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<IEnumerable<Guid>>.Success(new[] { item1.Id }));

        var query = new SelectVideoEligibleItemsQuery(MinRelevanceScore: 85, MaxItems: 5);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var data = result.Data!;
        Assert.Single(data);
        Assert.Equal(item2.Id, data[0].NewsItemId);
    }

    [Fact]
    public async Task Handle_NoEligibleItems_ReturnsEmptyList()
    {
        // All categories return empty (set in constructor default)
        var query = new SelectVideoEligibleItemsQuery(MinRelevanceScore: 85, MaxItems: 5);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
    }
}
