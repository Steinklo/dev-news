using DevNews.Application.Common.Models;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.ShortVideo.Commands;

public record ValidateVideoScriptCommand(
    string Script,
    string OriginalSummary) : IRequest<ResultResponse<ScriptValidationResult>>;

public class ValidateVideoScriptHandler(
    IVideoScriptValidationService validationService,
    ILogger<ValidateVideoScriptHandler> logger)
    : IRequestHandler<ValidateVideoScriptCommand, ResultResponse<ScriptValidationResult>>
{
    public async ValueTask<ResultResponse<ScriptValidationResult>> Handle(
        ValidateVideoScriptCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Validating video script ({Length} chars)", request.Script.Length);

        var result = await validationService.ValidateScriptAsync(
            request.Script, request.OriginalSummary, cancellationToken);

        if (!result.IsSuccess)
        {
            logger.LogWarning("Script validation failed: {Error}", result.ErrorMessage);
            return result;
        }

        if (!result.Data!.IsValid)
        {
            logger.LogWarning("Script rejected (score: {Score}): {Reason}",
                result.Data.QualityScore, result.Data.Reason);
        }
        else
        {
            logger.LogInformation("Script validated (score: {Score})", result.Data.QualityScore);
        }

        return result;
    }
}
