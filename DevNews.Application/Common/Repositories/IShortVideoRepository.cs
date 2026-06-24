using DevNews.Domain.Common;

namespace DevNews.Application.Common.Repositories;

public interface IShortVideoRepository
{
    Task<ResultResponse<Domain.ShortVideo.ShortVideo>> AddAsync(
        Domain.ShortVideo.ShortVideo shortVideo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns NewsItem IDs that already have videos generated since the given date.
    /// Used to avoid duplicate video generation.
    /// </summary>
    Task<ResultResponse<IEnumerable<Guid>>> GetNewsItemIdsWithVideosAsync(
        DateTimeOffset since,
        CancellationToken cancellationToken = default);
}
