using DevNews.Application.Common.Services;
using DevNews.Application.ShortVideo.Commands;
using DevNews.Domain.Common;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DevNews.UnitTests.Application.ShortVideo.Commands;

public class GenerateVideoScriptHandlerTests
{
    private readonly IVideoScriptService _scriptService = Substitute.For<IVideoScriptService>();
    private readonly GenerateVideoScriptHandler _handler;

    public GenerateVideoScriptHandlerTests()
    {
        _handler = new GenerateVideoScriptHandler(
            _scriptService,
            NullLogger<GenerateVideoScriptHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ServiceSucceeds_ReturnsScript()
    {
        var script = TestData.ValidScript;
        _scriptService.GenerateScriptAsync("Test Title", "Test Summary", "AiModelsAndApis",
                Arg.Any<CancellationToken>())
            .Returns(ResultResponse<string>.Success(script));

        var command = new GenerateVideoScriptCommand("Test Title", "Test Summary", "AiModelsAndApis");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(script, result.Data);
    }

    [Fact]
    public async Task Handle_ServiceFails_ReturnsFailure()
    {
        _scriptService.GenerateScriptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(ResultResponse<string>.Failure("Script generation failed"));

        var command = new GenerateVideoScriptCommand("Test Title", "Test Summary", "AiModelsAndApis");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Script generation failed", result.ErrorMessage);
    }
}
