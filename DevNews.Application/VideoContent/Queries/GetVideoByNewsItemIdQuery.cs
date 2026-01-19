using DevNews.Application.Common.Repositories;
using DevNews.Application.VideoContent.Dtos;
using DevNews.Domain.Common;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.VideoContent.Queries;

/// <summary>
/// Query to get VideoContent by NewsItem ID.
/// </summary>
public record GetVideoByNewsItemIdQuery(Guid NewsItemId) : IRequest<ResultResponse<VideoContentDto?>>;

public class GetVideoByNewsItemIdHandler(
    IVideoContentRepository repository,
    ILogger<GetVideoByNewsItemIdHandler> logger) : IRequestHandler<GetVideoByNewsItemIdQuery, ResultResponse<VideoContentDto?>>
{
    public async ValueTask<ResultResponse<VideoContentDto?>> Handle(GetVideoByNewsItemIdQuery request, CancellationToken ct)
    {
        var videoContent = await repository.GetByNewsItemIdAsync(request.NewsItemId, ct);

        if (videoContent == null)
        {
            logger.LogInformation("No video found for NewsItem {NewsItemId}", request.NewsItemId);
            return ResultResponse<VideoContentDto?>.Success(null);
        }

        var dto = VideoContentDto.FromDomain(videoContent);
        return ResultResponse<VideoContentDto?>.Success(dto);
    }
}
