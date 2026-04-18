using DevNews.Application.Common.Services;
using DevNews.Application.SocialPost.Dtos;
using DevNews.Domain.Common;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.SocialPost.Commands;

public record GenerateSocialPostCommand(
    SocialPostEligibleItem Item) : IRequest<ResultResponse<string>>;

public class GenerateSocialPostHandler(
    ISocialPostGenerationService socialPostGenerationService,
    ILogger<GenerateSocialPostHandler> logger)
    : IRequestHandler<GenerateSocialPostCommand, ResultResponse<string>>
{
    public async ValueTask<ResultResponse<string>> Handle(
        GenerateSocialPostCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Generating social post for {Title}", request.Item.Title);

        var result = await socialPostGenerationService.GenerateSocialPostAsync(request.Item, cancellationToken);

        if (!result.IsSuccess)
            logger.LogWarning("Social post generation failed: {Error}", result.ErrorMessage);
        else
            logger.LogInformation("Social post generated successfully, length: {Length}", result.Data!.Length);

        return result;
    }
}
