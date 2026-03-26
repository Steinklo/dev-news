using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.ShortVideo.Commands;

public record GenerateVideoScriptCommand(
    string Title,
    string Summary,
    string Category) : IRequest<ResultResponse<string>>;

public class GenerateVideoScriptHandler(
    IVideoScriptService scriptService,
    ILogger<GenerateVideoScriptHandler> logger)
    : IRequestHandler<GenerateVideoScriptCommand, ResultResponse<string>>
{
    public async ValueTask<ResultResponse<string>> Handle(
        GenerateVideoScriptCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Generating video script for: {Title}", request.Title);

        var result = await scriptService.GenerateScriptAsync(
            request.Title, request.Summary, request.Category, cancellationToken);

        if (!result.IsSuccess)
        {
            logger.LogWarning("Failed to generate script for {Title}: {Error}",
                request.Title, result.ErrorMessage);
            return result;
        }

        logger.LogInformation("Generated video script for: {Title} ({Length} chars)",
            request.Title, result.Data!.Length);

        return result;
    }
}
