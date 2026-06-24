using DevNews.Application.Common.Repositories;
using DevNews.Application.ShortVideo.Commands;
using DevNews.Domain.Common;
using DevNews.Domain.Common.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DevNews.UnitTests.Application.ShortVideo.Commands;

public class PersistShortVideoHandlerTests
{
    private readonly IShortVideoRepository _repository = Substitute.For<IShortVideoRepository>();
    private readonly PersistShortVideoHandler _handler;

    public PersistShortVideoHandlerTests()
    {
        _handler = new PersistShortVideoHandler(
            _repository,
            NullLogger<PersistShortVideoHandler>.Instance);
    }

    [Fact]
    public async Task Handle_NoPublications_ReturnsGuid()
    {
        _repository.AddAsync(Arg.Any<DevNews.Domain.ShortVideo.ShortVideo>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var video = callInfo.ArgAt<DevNews.Domain.ShortVideo.ShortVideo>(0);
                return ResultResponse<DevNews.Domain.ShortVideo.ShortVideo>.Success(video);
            });

        var command = new PersistShortVideoCommand(
            NewsItemId: Guid.NewGuid(),
            Script: TestData.ValidScript,
            DurationSeconds: 30,
            VideoUrl: "https://storage.example.com/video.mp4",
            Publications: null);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Data);
    }

    [Fact]
    public async Task Handle_WithPublications_ReturnsGuid()
    {
        _repository.AddAsync(Arg.Any<DevNews.Domain.ShortVideo.ShortVideo>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var video = callInfo.ArgAt<DevNews.Domain.ShortVideo.ShortVideo>(0);
                return ResultResponse<DevNews.Domain.ShortVideo.ShortVideo>.Success(video);
            });

        var publications = new List<PublicationInput>
        {
            new(Platform.YouTube, "yt-123", "https://youtube.com/watch?v=123"),
            new(Platform.LinkedIn, "li-456", "https://linkedin.com/posts/456")
        };

        var command = new PersistShortVideoCommand(
            NewsItemId: Guid.NewGuid(),
            Script: TestData.ValidScript,
            DurationSeconds: 30,
            VideoUrl: "https://storage.example.com/video.mp4",
            Publications: publications);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Data);
    }

    [Fact]
    public async Task Handle_RepositoryFails_ReturnsFailure()
    {
        _repository.AddAsync(Arg.Any<DevNews.Domain.ShortVideo.ShortVideo>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<DevNews.Domain.ShortVideo.ShortVideo>.Failure("Cosmos DB error"));

        var command = new PersistShortVideoCommand(
            NewsItemId: Guid.NewGuid(),
            Script: TestData.ValidScript,
            DurationSeconds: 30,
            VideoUrl: "https://storage.example.com/video.mp4",
            Publications: null);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Cosmos DB error", result.ErrorMessage);
    }
}
