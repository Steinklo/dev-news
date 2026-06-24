using DevNews.Application.Common.Models;
using DevNews.Domain.Common;

namespace DevNews.Application.Common.Services;

public interface ISocialPostPublisher
{
    /// <summary>Human-readable platform name, e.g. "LinkedIn" or "Bluesky" (for logging).</summary>
    string PlatformName { get; }

    Task<ResultResponse<PlatformPublishResult>> PublishTextAsync(
        string text,
        CancellationToken ct = default);
}
