using DevNews.Application.Common.Repositories;
using DevNews.Application.ShortVideo.Queries;
using DevNews.Domain.Common;
using DevNews.Domain.NewsItem.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DevNews.UnitTests.Application.ShortVideo.Queries;

public class SelectDailyVideoItemsHandlerTests
{
    private readonly INewsItemRepository _newsItemRepository = Substitute.For<INewsItemRepository>();
    private readonly IShortVideoRepository _shortVideoRepository = Substitute.For<IShortVideoRepository>();
    private readonly SelectDailyVideoItemsHandler _handler;

    public SelectDailyVideoItemsHandlerTests()
    {
        _handler = new SelectDailyVideoItemsHandler(
            _newsItemRepository,
            _shortVideoRepository,
            NullLogger<SelectDailyVideoItemsHandler>.Instance);

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
    public async Task Handle_PicksSingleHighestRelevanceItem()
    {
        var item90 = TestData.CreateValidNewsItem(CategoryEnum.AiModelsAndApis, relevanceScore: 90);
        var item95 = TestData.CreateValidNewsItem(CategoryEnum.AiModelsAndApis, relevanceScore: 95);

        _newsItemRepository.GetByCategoryAndMonthAsync(
                CategoryEnum.AiModelsAndApis, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<IEnumerable<DevNews.Domain.NewsItem.NewsItem>>.Success(
                new[] { item90, item95 }));

        var result = await _handler.Handle(new SelectDailyVideoItemsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        Assert.Equal(95, result.Data![0].RelevanceScore);
        Assert.Equal(item95.Id, result.Data[0].NewsItemId);
    }

    [Fact]
    public async Task Handle_ItemAlreadyVideoedThisMonth_IsExcluded()
    {
        // The month's top scorer already has a video; the next-best un-videoed item must win instead,
        // so the same article is never rendered twice (the regression this dedup fixes).
        var item90 = TestData.CreateValidNewsItem(CategoryEnum.AiModelsAndApis, relevanceScore: 90);
        var item95 = TestData.CreateValidNewsItem(CategoryEnum.AiModelsAndApis, relevanceScore: 95);

        _newsItemRepository.GetByCategoryAndMonthAsync(
                CategoryEnum.AiModelsAndApis, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<IEnumerable<DevNews.Domain.NewsItem.NewsItem>>.Success(
                new[] { item90, item95 }));

        _shortVideoRepository.GetNewsItemIdsWithVideosAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<IEnumerable<Guid>>.Success(new[] { item95.Id }));

        var result = await _handler.Handle(new SelectDailyVideoItemsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        Assert.Equal(item90.Id, result.Data![0].NewsItemId);
    }

    [Fact]
    public async Task Handle_DedupLookup_UsesStartOfCurrentMonth()
    {
        // The dedup window must span the whole month (the candidate pool does), not a rolling 24h
        // window — otherwise the month's top scorer slips back in and re-renders on later days.
        await _handler.Handle(new SelectDailyVideoItemsQuery(), CancellationToken.None);

        await _shortVideoRepository.Received(1).GetNewsItemIdsWithVideosAsync(
            Arg.Is<DateTimeOffset>(d => d.Day == 1 && d.TimeOfDay == TimeSpan.Zero && d.Offset == TimeSpan.Zero),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DedupLookupFails_FailsClosed()
    {
        // A high-scoring candidate exists, but the dedup lookup fails. The handler must NOT proceed
        // (which would risk a duplicate publish); it fails so the activity skips the run.
        var item95 = TestData.CreateValidNewsItem(CategoryEnum.AiModelsAndApis, relevanceScore: 95);

        _newsItemRepository.GetByCategoryAndMonthAsync(
                CategoryEnum.AiModelsAndApis, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<IEnumerable<DevNews.Domain.NewsItem.NewsItem>>.Success(
                new[] { item95 }));

        _shortVideoRepository.GetNewsItemIdsWithVideosAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<IEnumerable<Guid>>.Failure("cosmos unavailable"));

        var result = await _handler.Handle(new SelectDailyVideoItemsQuery(), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_AllCandidatesAlreadyVideoed_ReturnsEmpty()
    {
        var item95 = TestData.CreateValidNewsItem(CategoryEnum.AiModelsAndApis, relevanceScore: 95);

        _newsItemRepository.GetByCategoryAndMonthAsync(
                CategoryEnum.AiModelsAndApis, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<IEnumerable<DevNews.Domain.NewsItem.NewsItem>>.Success(
                new[] { item95 }));

        _shortVideoRepository.GetNewsItemIdsWithVideosAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<IEnumerable<Guid>>.Success(new[] { item95.Id }));

        var result = await _handler.Handle(new SelectDailyVideoItemsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task Handle_ItemsBelowFloor_AreExcluded()
    {
        var item84 = TestData.CreateValidNewsItem(CategoryEnum.AiModelsAndApis, relevanceScore: 84);

        _newsItemRepository.GetByCategoryAndMonthAsync(
                CategoryEnum.AiModelsAndApis, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<IEnumerable<DevNews.Domain.NewsItem.NewsItem>>.Success(
                new[] { item84 }));

        var result = await _handler.Handle(new SelectDailyVideoItemsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task Handle_NoCandidates_ReturnsEmpty()
    {
        var result = await _handler.Handle(new SelectDailyVideoItemsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
    }
}
