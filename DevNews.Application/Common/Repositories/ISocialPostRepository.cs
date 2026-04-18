using DevNews.Domain.Common;

namespace DevNews.Application.Common.Repositories;

public interface ISocialPostRepository
{
    Task<ResultResponse<Domain.SocialPost.SocialPost>> AddAsync(
        Domain.SocialPost.SocialPost socialPost,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns NewsItem IDs that already have social posts for the given date.
    /// Used to avoid duplicate social post generation.
    /// </summary>
    Task<ResultResponse<IEnumerable<Guid>>> GetNewsItemIdsWithPostsAsync(
        DateOnly date,
        CancellationToken cancellationToken = default);
}
