using DevNews.Application.Common.Repositories;
using DevNews.Application.VideoContent.Dtos;
using DevNews.Domain.Common;
using DevNews.Domain.VideoContent.Enums;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.VideoContent.Queries;

/// <summary>
/// Query to get all VideoContent items by status.
/// </summary>
public record GetVideosByStatusQuery(VideoStatusEnum Status) : IRequest<ResultResponse<IReadOnlyList<VideoContentDto>>>;

public class GetVideosByStatusHandler(
    IVideoContentRepository repository,
    ILogger<GetVideosByStatusHandler> logger) : IRequestHandler<GetVideosByStatusQuery, ResultResponse<IReadOnlyList<VideoContentDto>>>
{
    public async ValueTask<ResultResponse<IReadOnlyList<VideoContentDto>>> Handle(GetVideosByStatusQuery request, CancellationToken ct)
    {
        var videoContents = await repository.GetByStatusAsync(request.Status, ct);

        var dtos = videoContents
            .Select(VideoContentDto.FromDomain)
            .ToList();

        logger.LogInformation("Found {Count} videos with status {Status}", dtos.Count, request.Status);

        return ResultResponse<IReadOnlyList<VideoContentDto>>.Success(dtos);
    }
}
