using DevNews.Application.SocialPost.Dtos;
using DevNews.Domain.Common;

namespace DevNews.Application.Common.Services;

public interface ISocialPostGenerationService
{
    Task<ResultResponse<string>> GenerateSocialPostAsync(
        SocialPostEligibleItem item,
        CancellationToken ct = default);
}
