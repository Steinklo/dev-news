using DevNews.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DevNews.UnitTests.Infrastructure.Services;

public class BlueskyPublisherTests
{
    private static BlueskyPublisher Create(string? handle, string? appPassword)
    {
        var config = Substitute.For<IConfiguration>();
        config["BlueskyHandle"].Returns(handle);
        config["BlueskyAppPassword"].Returns(appPassword);
        return new BlueskyPublisher(new HttpClient(), config, NullLogger<BlueskyPublisher>.Instance);
    }

    [Fact]
    public void PlatformName_IsBluesky()
    {
        Assert.Equal("Bluesky", Create("handle", "pw").PlatformName);
    }

    [Fact]
    public async Task PublishTextAsync_NoCredentials_ReturnsFailure_WithoutThrowing()
    {
        var sut = Create(null, null);

        var result = await sut.PublishTextAsync("a perfectly fine short post", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("not configured", result.ErrorMessage);
    }
}
