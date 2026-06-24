using DevNews.Application.ShortVideo.Dtos;
using DevNews.Application.ShortVideo.Queries;
using Mediator;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DevNews.Functions.DailyVideo;

public class Activities
{
    private readonly IMediator _mediator;
    private readonly ILogger<Activities> _logger;

    public Activities(IMediator mediator, ILogger<Activities> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [Function(nameof(SelectDailyVideoItemsActivity))]
    public async Task<List<DailyVideoItem>> SelectDailyVideoItemsActivity(
        [ActivityTrigger] object? input,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Activity: Selecting the top item for the daily video");

        var result = await _mediator.Send(new SelectDailyVideoItemsQuery(), cancellationToken);

        if (!result.IsSuccess)
            throw new InvalidOperationException($"Daily video selection failed: {result.ErrorMessage}");

        return result.Data!.ToList();
    }
}
