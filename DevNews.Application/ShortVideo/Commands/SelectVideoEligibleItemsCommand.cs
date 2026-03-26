using DevNews.Application.Common.Repositories;
using DevNews.Application.ShortVideo.Dtos;
using DevNews.Domain.Common;
using DevNews.Domain.NewsItem.Enums;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.ShortVideo.Commands;

public record SelectVideoEligibleItemsCommand(
    int MinRelevanceScore = 85,
    int MaxItems = 5) : IRequest<ResultResponse<IReadOnlyList<VideoEligibleItem>>>;

public class SelectVideoEligibleItemsHandler(
    INewsItemRepository newsItemRepository,
    IShortVideoRepository shortVideoRepository,
    ILogger<SelectVideoEligibleItemsHandler> logger)
    : IRequestHandler<SelectVideoEligibleItemsCommand, ResultResponse<IReadOnlyList<VideoEligibleItem>>>
{
    public async ValueTask<ResultResponse<IReadOnlyList<VideoEligibleItem>>> Handle(
        SelectVideoEligibleItemsCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Selecting video-eligible items (min score: {MinScore}, max: {MaxItems})",
            request.MinRelevanceScore, request.MaxItems);

        var since = DateTimeOffset.UtcNow.AddHours(-24);

        // Get IDs of news items that already have videos
        var existingResult = await shortVideoRepository.GetNewsItemIdsWithVideosAsync(since, cancellationToken);
        var existingIds = existingResult.IsSuccess
            ? existingResult.Data!.ToHashSet()
            : new HashSet<Guid>();

        // Query high-relevance items from all categories in the last 24 hours
        var eligibleItems = new List<VideoEligibleItem>();
        var now = DateTimeOffset.UtcNow;

        foreach (var category in Enum.GetValues<CategoryEnum>())
        {
            var result = await newsItemRepository.GetByCategoryAndMonthAsync(
                category, since, now, limit: 50, cancellationToken);

            if (!result.IsSuccess) continue;

            foreach (var item in result.Data!)
            {
                if (item.RelevanceScore.Value >= request.MinRelevanceScore
                    && !existingIds.Contains(item.Id))
                {
                    eligibleItems.Add(new VideoEligibleItem(
                        item.Id,
                        item.Title.Value,
                        item.Summary.Value,
                        category.ToString(),
                        item.RelevanceScore.Value,
                        item.Tags.ToList()));
                }
            }
        }

        // Sort by relevance and take top N
        var selected = eligibleItems
            .OrderByDescending(x => x.RelevanceScore)
            .Take(request.MaxItems)
            .ToList();

        logger.LogInformation("Selected {Count} video-eligible items from {Total} candidates",
            selected.Count, eligibleItems.Count);

        return ResultResponse<IReadOnlyList<VideoEligibleItem>>.Success(selected);
    }
}
