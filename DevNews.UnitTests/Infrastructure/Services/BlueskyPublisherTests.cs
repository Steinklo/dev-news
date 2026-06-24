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

    [Fact]
    public void BuildFacets_WithUrl_ComputesUtf8ByteRange()
    {
        var url = "https://example.com/x";
        var facets = BlueskyPublisher.BuildFacets("Hello world " + url);

        Assert.NotNull(facets);
        Assert.Single(facets!);
        var facet = (Dictionary<string, object?>)facets![0];
        var index = (Dictionary<string, object?>)facet["index"]!;
        Assert.Equal(12, index["byteStart"]); // "Hello world " = 12 ASCII bytes
        Assert.Equal(12 + url.Length, index["byteEnd"]);
    }

    [Fact]
    public void BuildFacets_NoUrl_ReturnsNull()
    {
        Assert.Null(BlueskyPublisher.BuildFacets("just text, no link"));
    }
}
