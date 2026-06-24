using DevNews.Application.Common.Services;
using DevNews.Application.SocialPost.Commands;
using DevNews.Application.SocialPost.Dtos;
using DevNews.Domain.Common;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DevNews.UnitTests.Application.SocialPost.Commands;

public class GenerateSocialPostHandlerTests
{
    private readonly ISocialPostGenerationService _service = Substitute.For<ISocialPostGenerationService>();
    private readonly GenerateSocialPostHandler _handler;

    public GenerateSocialPostHandlerTests()
    {
        _handler = new GenerateSocialPostHandler(_service, NullLogger<GenerateSocialPostHandler>.Instance);
    }

    private static SocialPostEligibleItem Item() =>
        new(Guid.NewGuid(), "Title", "Summary", "AiModelsAndApis", 90, new List<string>(), "https://example.com/a");

    [Fact]
    public async Task Handle_ValidText_ReturnsTrimmedText()
    {
        var text = new string('a', 150);
        _service.GenerateSocialPostAsync(Arg.Any<SocialPostEligibleItem>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<string>.Success($"  {text}  "));

        var result = await _handler.Handle(new GenerateSocialPostCommand(Item()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(text, result.Data); // trimmed and validated via SocialPostText
    }

    [Fact]
    public async Task Handle_TooShortText_ReturnsFailure_SoItIsNeverPublished()
    {
        _service.GenerateSocialPostAsync(Arg.Any<SocialPostEligibleItem>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<string>.Success("too short"));

        var result = await _handler.Handle(new GenerateSocialPostCommand(Item()), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_ServiceFails_ReturnsFailure()
    {
        _service.GenerateSocialPostAsync(Arg.Any<SocialPostEligibleItem>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<string>.Failure("AI unavailable"));

        var result = await _handler.Handle(new GenerateSocialPostCommand(Item()), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("AI unavailable", result.ErrorMessage);
    }
}
