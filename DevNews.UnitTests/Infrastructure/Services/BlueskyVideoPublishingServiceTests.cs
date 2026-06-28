using System.Text.Json;
using DevNews.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DevNews.UnitTests.Infrastructure.Services;

public class BlueskyVideoPublishingServiceTests
{
    private static BlueskyVideoPublishingService Create(string? handle, string? appPassword)
    {
        var config = Substitute.For<IConfiguration>();
        config["BlueskyHandle"].Returns(handle);
        config["BlueskyAppPassword"].Returns(appPassword);
        return new BlueskyVideoPublishingService(
            new HttpClient(), config, NullLogger<BlueskyVideoPublishingService>.Instance);
    }

    [Fact]
    public async Task PublishAsync_NoCredentials_ReturnsFailure_WithoutThrowing()
    {
        var sut = Create(null, null);

        var result = await sut.PublishAsync(
            "https://storage.example.com/video.mp4", "Title", "Description", [], CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("not configured", result.ErrorMessage);
    }

    [Fact]
    public void ResolvePdsHost_WithAtprotoPdsService_ReturnsEndpointHost()
    {
        var session = JsonDocument.Parse("""
            {
              "did": "did:plc:abc",
              "didDoc": {
                "service": [
                  { "id": "#atproto_pds", "type": "AtprotoPersonalDataServer",
                    "serviceEndpoint": "https://shimeji.us-east.host.bsky.network" }
                ]
              }
            }
            """).RootElement;

        Assert.Equal("shimeji.us-east.host.bsky.network", BlueskyVideoPublishingService.ResolvePdsHost(session));
    }

    [Fact]
    public void ResolvePdsHost_WithoutDidDoc_FallsBackToBskySocial()
    {
        var session = JsonDocument.Parse("""{ "did": "did:plc:abc" }""").RootElement;

        Assert.Equal("bsky.social", BlueskyVideoPublishingService.ResolvePdsHost(session));
    }
}
