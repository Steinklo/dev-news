using DevNews.Application.Common.Services;
using DevNews.Application.SocialPost.Dtos;
using DevNews.Domain.Common;
using DevNews.Domain.SocialPost.ValueObjects;
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
        {
            logger.LogWarning("Social post generation failed: {Error}", result.ErrorMessage);
            return result;
        }

        // Enforce the SocialPostText invariant BEFORE the text is published, not just at persist time.
        // Returns the trimmed, validated text so what we publish equals what we store.
        var validation = SocialPostText.Create(result.Data!);
        if (!validation.IsSuccess)
        {
            logger.LogWarning("Generated social post failed validation, will not publish: {Error}", validation.ErrorMessage);
            return ResultResponse<string>.Failure(validation.ErrorMessage);
        }

        logger.LogInformation("Social post generated successfully, length: {Length}", validation.Data!.Value.Length);
        return ResultResponse<string>.Success(validation.Data!.Value);
    }
}
