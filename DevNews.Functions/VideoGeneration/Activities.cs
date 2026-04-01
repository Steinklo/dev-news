using DevNews.Application.Common.Models;
using DevNews.Application.ShortVideo.Commands;
using DevNews.Application.ShortVideo.Queries;
using DevNews.Application.ShortVideo.Dtos;
using DevNews.Domain.ShortVideo.Enums;
using Mediator;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DevNews.Functions.VideoGeneration;

public class Activities
{
    private readonly IMediator _mediator;
    private readonly ILogger<Activities> _logger;

    public Activities(IMediator mediator, ILogger<Activities> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [Function(nameof(SelectEligibleItemsActivity))]
    public async Task<List<VideoEligibleItem>> SelectEligibleItemsActivity(
        [ActivityTrigger] object? input,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Activity: Selecting video-eligible items");

        var result = await _mediator.Send(new SelectVideoEligibleItemsQuery(), cancellationToken);

        if (!result.IsSuccess)
            throw new InvalidOperationException($"Selection failed: {result.ErrorMessage}");

        return result.Data!.ToList();
    }

    [Function(nameof(GenerateScriptActivity))]
    public async Task<string?> GenerateScriptActivity(
        [ActivityTrigger] ScriptGenerationInput input,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Activity: Generating script for {Title}", input.Title);

        var result = await _mediator.Send(
            new GenerateVideoScriptCommand(input.Title, input.Summary, input.Category),
            cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Script generation failed for {Title}: {Error}",
                input.Title, result.ErrorMessage);
            return null;
        }

        return result.Data;
    }

    [Function(nameof(ValidateScriptActivity))]
    public async Task<ScriptValidationResult?> ValidateScriptActivity(
        [ActivityTrigger] ScriptValidationInput input,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Activity: Validating script");

        var result = await _mediator.Send(
            new ValidateVideoScriptCommand(input.Script, input.OriginalSummary),
            cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Script validation failed: {Error}", result.ErrorMessage);
            return null;
        }

        return result.Data;
    }

    [Function(nameof(GenerateVideoActivity))]
    public async Task<GeneratedVideoOutput?> GenerateVideoActivity(
        [ActivityTrigger] VideoGenerationInput input,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Activity: Generating video for {NewsItemId}", input.NewsItemId);

        var result = await _mediator.Send(
            new GenerateVideoCommand(input.NewsItemId, input.Script, input.Title),
            cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Video generation failed for {NewsItemId}: {Error}",
                input.NewsItemId, result.ErrorMessage);
            return null;
        }

        return new GeneratedVideoOutput(result.Data!.VideoUrl, result.Data.DurationSeconds);
    }

    [Function(nameof(PublishVideoActivity))]
    public async Task<PublishOutput?> PublishVideoActivity(
        [ActivityTrigger] PublishInput input,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Activity: Publishing video to {Platform}", input.Platform);

        var result = await _mediator.Send(
            new PublishVideoCommand(input.VideoUrl, input.Title, input.Description, input.Tags, input.Platform),
            cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Publishing to {Platform} failed: {Error}",
                input.Platform, result.ErrorMessage);
            return null;
        }

        return new PublishOutput(input.Platform, result.Data!.ExternalId, result.Data.PublishedUrl);
    }

    [Function(nameof(PersistShortVideoActivity))]
    public async Task<Guid?> PersistShortVideoActivity(
        [ActivityTrigger] PersistVideoInput input,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Activity: Persisting ShortVideo for {NewsItemId}", input.NewsItemId);

        var publications = input.Publications?
            .Select(p => new PublicationInput(p.Platform, p.ExternalId, p.PublishedUrl))
            .ToList();

        var result = await _mediator.Send(
            new PersistShortVideoCommand(
                input.NewsItemId,
                input.Script,
                input.DurationSeconds,
                input.VideoUrl,
                publications),
            cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogError("Persist failed for {NewsItemId}: {Error}",
                input.NewsItemId, result.ErrorMessage);
            return null;
        }

        return result.Data;
    }
}
