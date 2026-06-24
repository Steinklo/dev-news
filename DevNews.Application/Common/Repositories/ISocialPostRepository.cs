using DevNews.Domain.Common;

namespace DevNews.Application.Common.Repositories;

public interface ISocialPostRepository
{
    Task<ResultResponse<Domain.SocialPost.SocialPost>> AddAsync(
        Domain.SocialPost.SocialPost socialPost,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns NewsItem IDs that already have a social post in the given month's partition,
    /// regardless of status. Used to avoid duplicate social post generation across the month
    /// (not just the current calendar day).
    /// </summary>
    Task<ResultResponse<IEnumerable<Guid>>> GetNewsItemIdsWithPostsThisMonthAsync(
        DateOnly month,
        CancellationToken cancellationToken = default);
}
