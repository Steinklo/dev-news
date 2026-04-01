using DevNews.Application.Common.Models;
using DevNews.Application.Common.Services;
using DevNews.Application.ShortVideo.Commands;
using DevNews.Domain.Common;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DevNews.UnitTests.Application.ShortVideo.Commands;

public class ValidateVideoScriptHandlerTests
{
    private readonly IVideoScriptValidationService _validationService =
        Substitute.For<IVideoScriptValidationService>();
    private readonly ValidateVideoScriptHandler _handler;

    public ValidateVideoScriptHandlerTests()
    {
        _handler = new ValidateVideoScriptHandler(
            _validationService,
            NullLogger<ValidateVideoScriptHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ValidScript_ReturnsIsValidTrue()
    {
        var validation = new ScriptValidationResult(IsValid: true, Reason: null, QualityScore: 90);
        _validationService.ValidateScriptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<ScriptValidationResult>.Success(validation));

        var command = new ValidateVideoScriptCommand(TestData.ValidScript, TestData.ValidSummary);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Data!.IsValid);
        Assert.Equal(90, result.Data.QualityScore);
    }

    [Fact]
    public async Task Handle_InvalidScript_ReturnsIsValidFalse()
    {
        var validation = new ScriptValidationResult(IsValid: false, Reason: "Too much hallucination", QualityScore: 30);
        _validationService.ValidateScriptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<ScriptValidationResult>.Success(validation));

        var command = new ValidateVideoScriptCommand(TestData.ValidScript, TestData.ValidSummary);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Data!.IsValid);
        Assert.Equal("Too much hallucination", result.Data.Reason);
    }

    [Fact]
    public async Task Handle_ServiceFails_ReturnsFailure()
    {
        _validationService.ValidateScriptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ResultResponse<ScriptValidationResult>.Failure("Validation service unavailable"));

        var command = new ValidateVideoScriptCommand(TestData.ValidScript, TestData.ValidSummary);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Validation service unavailable", result.ErrorMessage);
    }
}
