using DevNews.Application.Common.Models;
using DevNews.Application.Common.Services;
using DevNews.Application.ShortVideo.Commands;
using DevNews.Domain.Common;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DevNews.UnitTests.Application.ShortVideo.Commands;

public class GenerateVideoHandlerTests
{
    private readonly IVideoGenerationService _videoGenService = Substitute.For<IVideoGenerationService>();
    private readonly IVideoStorageService _videoStorageService = Substitute.For<IVideoStorageService>();
    private readonly GenerateVideoHandler _handler;

    public GenerateVideoHandlerTests()
    {
        _handler = new GenerateVideoHandler(
            _videoGenService,
            _videoStorageService,
            NullLogger<GenerateVideoHandler>.Instance);
    }

    [Fact]
    public async Task Handle_GenerateAndUploadSucceed_ReturnsUrlAndDuration()
    {
        var newsItemId = Guid.NewGuid();
        var videoData = new byte[] { 0x01, 0x02, 0x03 };
        var generated = new GeneratedVideo(videoData, DurationSeconds: 30, ContentType: "video/mp4");

        _videoGenService.GenerateVideoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<GeneratedVideo>.Success(generated));
        _videoStorageService.UploadVideoAsync(videoData, Arg.Any<string>(), "video/mp4", Arg.Any<CancellationToken>())
            .Returns(ResultResponse<string>.Success("https://storage.example.com/video.mp4"));

        var command = new GenerateVideoCommand(newsItemId, TestData.ValidScript, "Test Title");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("https://storage.example.com/video.mp4", result.Data!.VideoUrl);
        Assert.Equal(30, result.Data.DurationSeconds);
    }

    [Fact]
    public async Task Handle_GenerationFails_ReturnsFailure()
    {
        _videoGenService.GenerateVideoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<GeneratedVideo>.Failure("Video generation error"));

        var command = new GenerateVideoCommand(Guid.NewGuid(), TestData.ValidScript, "Test Title");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Video generation error", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_UploadFails_ReturnsFailure()
    {
        var generated = new GeneratedVideo(new byte[] { 0x01 }, DurationSeconds: 30, ContentType: "video/mp4");
        _videoGenService.GenerateVideoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<GeneratedVideo>.Success(generated));
        _videoStorageService.UploadVideoAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(ResultResponse<string>.Failure("Upload failed"));

        var command = new GenerateVideoCommand(Guid.NewGuid(), TestData.ValidScript, "Test Title");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Upload failed", result.ErrorMessage);
    }
}
