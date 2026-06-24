using DevNews.Application.Common.Repositories;
using DevNews.Application.ShortVideo.Dtos;
using DevNews.Domain.Common;
using DevNews.Domain.NewsItem.Enums;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.ShortVideo.Queries;

/// <summary>
/// Selects the single highest-relevance news item of the day for the daily video. Independent of
/// social-post dedup. Returns 0 or 1 item: the top scorer at or above <see cref="MinRelevanceScore"/>,
/// or none if nothing clears the floor (a thin news day produces no video).
/// </summary>
public record SelectDailyVideoItemsQuery(
    int MinRelevanceScore = 85,
    int MaxItems = 1) : IRequest<ResultResponse<IReadOnlyList<DailyVideoItem>>>;

public class SelectDailyVideoItemsHandler(
    INewsItemRepository newsItemRepository,
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

        var items = new List<DailyVideoItem>();

        foreach (var category in Enum.GetValues<CategoryEnum>())
        {
            var result = await newsItemRepository.GetByCategoryAndMonthAsync(
                category, since, now, limit: 50, cancellationToken);

            if (!result.IsSuccess) continue;

            foreach (var item in result.Data!)
            {
                if (item.RelevanceScore.Value >= request.MinRelevanceScore)
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
