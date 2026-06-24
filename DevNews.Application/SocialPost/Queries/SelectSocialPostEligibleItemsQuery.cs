using DevNews.Application.Common.Repositories;
using DevNews.Application.SocialPost.Dtos;
using DevNews.Domain.Common;
using DevNews.Domain.NewsItem.Enums;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.SocialPost.Queries;

public record SelectSocialPostEligibleItemsQuery(
    int MinRelevanceScore = 85,
    int MaxItems = 5) : IRequest<ResultResponse<IReadOnlyList<SocialPostEligibleItem>>>;

public class SelectSocialPostEligibleItemsHandler(
    INewsItemRepository newsItemRepository,
    ISocialPostRepository socialPostRepository,
    ILogger<SelectSocialPostEligibleItemsHandler> logger)
    : IRequestHandler<SelectSocialPostEligibleItemsQuery, ResultResponse<IReadOnlyList<SocialPostEligibleItem>>>
{
    public async ValueTask<ResultResponse<IReadOnlyList<SocialPostEligibleItem>>> Handle(
        SelectSocialPostEligibleItemsQuery request,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Selecting social-post-eligible items (min score: {MinScore}, max: {MaxItems})",
            request.MinRelevanceScore, request.MaxItems);

        var since = DateTimeOffset.UtcNow.AddHours(-24);
        var now = DateTimeOffset.UtcNow;

        // Dedup against every post made this month (not just today), so an item posted late
        // yesterday is not re-posted early today. Known minor edge: at a month boundary an item
        // posted on the last day can repost on the first (different partition) — acceptable.
        var month = DateOnly.FromDateTime(DateTime.UtcNow);
        var existingResult = await socialPostRepository.GetNewsItemIdsWithPostsThisMonthAsync(month, cancellationToken);
        var existingIds = existingResult.IsSuccess
            ? existingResult.Data!.ToHashSet()
            : new HashSet<Guid>();

        var eligibleItems = new List<SocialPostEligibleItem>();

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
                    eligibleItems.Add(new SocialPostEligibleItem(
                        item.Id,
                        item.Title.Value,
                        item.Summary.Value,
                        category.ToString(),
                        item.RelevanceScore.Value,
                        item.Tags.ToList(),
                        item.Url.Value));
                }
            }
        }

        var selected = eligibleItems
            .OrderByDescending(x => x.RelevanceScore)
            .Take(request.MaxItems)
            .ToList();

        logger.LogInformation("Selected {Count} social-post-eligible items from {Total} candidates",
            selected.Count, eligibleItems.Count);

        return ResultResponse<IReadOnlyList<SocialPostEligibleItem>>.Success(selected);
    }
}
