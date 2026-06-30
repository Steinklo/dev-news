using DevNews.Application.Common.Repositories;
using DevNews.Application.ShortVideo.Dtos;
using DevNews.Domain.Common;
using DevNews.Domain.NewsItem.Enums;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.ShortVideo.Queries;

/// <summary>
/// Selects the single highest-relevance news item of the day for the daily video. Excludes items
/// that already have a video this month, so the same article is never rendered and published twice.
/// Returns 0 or 1 item: the top un-videoed scorer at or above <see cref="MinRelevanceScore"/>,
/// or none if nothing clears the floor (a thin news day produces no video).
/// </summary>
public record SelectDailyVideoItemsQuery(
    int MinRelevanceScore = 85,
    int MaxItems = 1) : IRequest<ResultResponse<IReadOnlyList<DailyVideoItem>>>;

public class SelectDailyVideoItemsHandler(
    INewsItemRepository newsItemRepository,
    IShortVideoRepository shortVideoRepository,
    ILogger<SelectDailyVideoItemsHandler> logger)
    : IRequestHandler<SelectDailyVideoItemsQuery, ResultResponse<IReadOnlyList<DailyVideoItem>>>
{
    public async ValueTask<ResultResponse<IReadOnlyList<DailyVideoItem>>> Handle(
        SelectDailyVideoItemsQuery request,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Selecting daily-video items (min score: {MinScore}, max: {MaxItems})",
            request.MinRelevanceScore, request.MaxItems);

        var since = DateTimeOffset.UtcNow.AddHours(-24);
        var now = DateTimeOffset.UtcNow;

        // Dedup against every video produced this month, not just the last 24h. The candidate pool
        // spans the whole month, so a narrower window would let the month's top scorer be re-selected
        // — and re-rendered/re-published — on later days. Mirrors the social-post selector's dedup.
        // Anchor on `since`, not `now`: the candidate query picks its month partition from `since`,
        // so on the 1st (when `since` falls in the prior month) the dedup window must too, or an
        // article videoed last month could be re-selected on the boundary run.
        var monthStart = new DateTimeOffset(since.Year, since.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var existingResult = await shortVideoRepository.GetNewsItemIdsWithVideosAsync(monthStart, cancellationToken);
        if (!existingResult.IsSuccess)
        {
            // Fail closed: without the dedup set we could re-render and re-publish a video for an
            // article that already has one. A missed daily video is recoverable; a duplicate publish
            // to YouTube/LinkedIn/Bluesky is not. The activity turns this failure into a skipped run.
            logger.LogError("Daily video dedup lookup failed, skipping selection: {Error}",
                existingResult.ErrorMessage);
            return ResultResponse<IReadOnlyList<DailyVideoItem>>.Failure(
                $"Could not load existing video IDs for dedup: {existingResult.ErrorMessage}");
        }

        var existingIds = existingResult.Data!.ToHashSet();

        var items = new List<DailyVideoItem>();

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
                    items.Add(new DailyVideoItem(
                        item.Id,
                        item.Title.Value,
                        item.Summary.Value,
                        category.ToString(),
                        item.RelevanceScore.Value));
                }
            }
        }

        var selected = items
            .OrderByDescending(x => x.RelevanceScore)
            .Take(request.MaxItems)
            .ToList();

        logger.LogInformation("Selected {Count} items for daily video from {Total} candidates",
            selected.Count, items.Count);

        return ResultResponse<IReadOnlyList<DailyVideoItem>>.Success(selected);
    }
}
