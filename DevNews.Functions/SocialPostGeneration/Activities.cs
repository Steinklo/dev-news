using DevNews.Application.SocialPost.Commands;
using DevNews.Application.SocialPost.Dtos;
using DevNews.Application.SocialPost.Queries;
using Mediator;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DevNews.Functions.SocialPostGeneration;

public class Activities
{
    private readonly IMediator _mediator;
    private readonly ILogger<Activities> _logger;

    public Activities(
        IMediator mediator,
        ILogger<Activities> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [Function(nameof(SelectSocialPostEligibleItemsActivity))]
    public async Task<List<SocialPostEligibleItem>> SelectSocialPostEligibleItemsActivity(
        [ActivityTrigger] object? input,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Activity: Selecting social-post-eligible items");

        var result = await _mediator.Send(new SelectSocialPostEligibleItemsQuery(), cancellationToken);

        if (!result.IsSuccess)
            throw new InvalidOperationException($"Social post selection failed: {result.ErrorMessage}");

        return result.Data!.ToList();
    }

    [Function(nameof(GenerateSocialPostActivity))]
    public async Task<string?> GenerateSocialPostActivity(
        [ActivityTrigger] SocialPostEligibleItem item,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Activity: Generating social post for {Title}", item.Title);

        var result = await _mediator.Send(new GenerateSocialPostCommand(item), cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Social post generation failed for {Title}: {Error}", item.Title, result.ErrorMessage);
            return null;
        }

        return result.Data;
    }

    [Function(nameof(PublishSocialPostActivity))]
    public async Task<SocialPostPublishOutput?> PublishSocialPostActivity(
        [ActivityTrigger] string text,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Activity: Publishing social post to LinkedIn");

        var result = await _mediator.Send(new PublishSocialPostCommand(text), cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Social post publishing failed: {Error}", result.ErrorMessage);
            return null;
        }

        return new SocialPostPublishOutput(result.Data!.ExternalId, result.Data.PublishedUrl);
    }

    [Function(nameof(PersistSocialPostActivity))]
    public async Task<Guid?> PersistSocialPostActivity(
        [ActivityTrigger] PersistSocialPostInput input,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Activity: Persisting SocialPost for news item {NewsItemId}", input.NewsItemId);

        var result = await _mediator.Send(
            new PersistSocialPostCommand(
                input.NewsItemId, input.Content, input.SourceUrl, input.ExternalId, input.PublishedUrl, input.Published),
            cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogError("Persist SocialPost failed: {Error}", result.ErrorMessage);
            return null;
        }

        return result.Data;
    }
}
