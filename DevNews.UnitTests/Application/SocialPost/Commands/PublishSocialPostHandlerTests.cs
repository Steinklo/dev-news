using DevNews.Application.Common.Models;
using DevNews.Application.Common.Services;
using DevNews.Application.SocialPost.Commands;
using DevNews.Domain.Common;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DevNews.UnitTests.Application.SocialPost.Commands;

public class PublishSocialPostHandlerTests
{
    private static ISocialPostPublisher Publisher(string name, ResultResponse<PlatformPublishResult> result)
    {
        var p = Substitute.For<ISocialPostPublisher>();
        p.PlatformName.Returns(name);
        p.PublishTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(result);
        return p;
    }

    [Fact]
    public async Task Handle_FansOutToAll_ReturnsFirstSuccess()
    {
        var failing = Publisher("LinkedIn", ResultResponse<PlatformPublishResult>.Failure("not configured"));
        var ok = Publisher("Bluesky",
            ResultResponse<PlatformPublishResult>.Success(new PlatformPublishResult("id-1", "https://bsky.app/x")));

        var handler = new PublishSocialPostHandler(new[] { failing, ok }, NullLogger<PublishSocialPostHandler>.Instance);

        var result = await handler.Handle(new PublishSocialPostCommand("hello"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("https://bsky.app/x", result.Data!.PublishedUrl);
        // Every configured platform is attempted, even after one succeeds.
        await failing.Received(1).PublishTextAsync("hello", Arg.Any<CancellationToken>());
        await ok.Received(1).PublishTextAsync("hello", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NonePublished_ReturnsFailure()
    {
        var a = Publisher("LinkedIn", ResultResponse<PlatformPublishResult>.Failure("not configured"));
        var b = Publisher("Bluesky", ResultResponse<PlatformPublishResult>.Failure("not configured"));

        var handler = new PublishSocialPostHandler(new[] { a, b }, NullLogger<PublishSocialPostHandler>.Instance);

        var result = await handler.Handle(new PublishSocialPostCommand("hello"), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}
