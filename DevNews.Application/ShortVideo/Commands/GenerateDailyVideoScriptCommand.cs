using DevNews.Application.Common.Models;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.ShortVideo.Commands;

public record GenerateDailyVideoScriptCommand(
    IReadOnlyList<NewsArticleSummary> Items) : IRequest<ResultResponse<string>>;

public class GenerateDailyVideoScriptHandler(
    IVideoScriptService videoScriptService,
    ILogger<GenerateDailyVideoScriptHandler> logger)
    : IRequestHandler<GenerateDailyVideoScriptCommand, ResultResponse<string>>
{
    public async ValueTask<ResultResponse<string>> Handle(
        GenerateDailyVideoScriptCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Generating daily video script for {Count} items", request.Items.Count);

        var result = await videoScriptService.GenerateDailyVideoScriptAsync(request.Items, cancellationToken);

        if (!result.IsSuccess)
            logger.LogWarning("Daily video script generation failed: {Error}", result.ErrorMessage);
        else
            logger.LogInformation("Daily video script generated successfully, length: {Length}", result.Data!.Length);

        return result;
    }
}
