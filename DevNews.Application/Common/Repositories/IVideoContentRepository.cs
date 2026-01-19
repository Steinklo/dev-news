using DevNews.Domain.Common;
using DevNews.Domain.VideoContent.Enums;

namespace DevNews.Application.Common.Repositories;

/// <summary>
/// Repository interface for VideoContent aggregate.
/// </summary>
public interface IVideoContentRepository
{
    Task<Domain.VideoContent.VideoContent?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Domain.VideoContent.VideoContent?> GetByNewsItemIdAsync(Guid newsItemId, CancellationToken ct = default);
    Task<IEnumerable<Domain.VideoContent.VideoContent>> GetByStatusAsync(VideoStatusEnum status, CancellationToken ct = default);
    Task<ResultResponse<Domain.VideoContent.VideoContent>> AddAsync(Domain.VideoContent.VideoContent videoContent, CancellationToken ct = default);
    Task<ResultResponse<Domain.VideoContent.VideoContent>> UpdateAsync(Domain.VideoContent.VideoContent videoContent, CancellationToken ct = default);
}
