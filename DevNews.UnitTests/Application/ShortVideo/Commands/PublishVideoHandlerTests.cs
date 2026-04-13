using DevNews.Application.Common.Models;
using DevNews.Application.Common.Services;
using DevNews.Application.ShortVideo.Commands;
using DevNews.Domain.Common;
using DevNews.Domain.ShortVideo.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DevNews.UnitTests.Application.ShortVideo.Commands;

public class PublishVideoHandlerTests
{
    private readonly IPlatformPublishingService _publishingService =
        Substitute.For<IPlatformPublishingService>();
    private readonly PublishVideoHandler _handler;

    public PublishVideoHandlerTests()
    {
        _handler = new PublishVideoHandler(
            _publishingService,
            NullLogger<PublishVideoHandler>.Instance);
    }

    [Fact]
    public async Task Handle_PublishSucceeds_ReturnsResult()
    {
        var publishResult = new PlatformPublishResult("yt-12345", "https://youtube.com/watch?v=12345");
        _publishingService.PublishAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string[]>(), Platform.YouTube, Arg.Any<CancellationToken>())
            .Returns(ResultResponse<PlatformPublishResult>.Success(publishResult));

        var command = new PublishVideoCommand(
            "https://storage.example.com/video.mp4",
            "Test Title", "Test Description",
            new[] { "ai", "security" },
            Platform.YouTube);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("yt-12345", result.Data!.ExternalId);
        Assert.Equal("https://youtube.com/watch?v=12345", result.Data.PublishedUrl);
    }

    [Fact]
    public async Task Handle_PublishFails_ReturnsFailure()
    {
        _publishingService.PublishAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string[]>(), Arg.Any<Platform>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<PlatformPublishResult>.Failure("Platform API error"));

        var command = new PublishVideoCommand(
            "https://storage.example.com/video.mp4",
            "Test Title", "Test Description",
            new[] { "ai" },
            Platform.LinkedIn);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Platform API error", result.ErrorMessage);
    }
}
