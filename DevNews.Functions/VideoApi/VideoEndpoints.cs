using System.Net;
using DevNews.Application.VideoContent.Queries;
using DevNews.Domain.VideoContent.Enums;
using Mediator;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DevNews.Functions.VideoApi;

public class VideoEndpoints
{
    private readonly IMediator _mediator;
    private readonly ILogger<VideoEndpoints> _logger;

    public VideoEndpoints(IMediator mediator, ILogger<VideoEndpoints> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/v1/news/{newsItemId}/video
    /// Get video content for a specific news item.
    /// </summary>
    [Function("GetVideoByNewsItemId")]
    public async Task<HttpResponseData> GetVideoByNewsItemId(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/news/{newsItemId}/video")] HttpRequestData req,
        string newsItemId,
        CancellationToken ct)
    {
        if (!Guid.TryParse(newsItemId, out var guid))
        {
            _logger.LogWarning("Invalid NewsItem ID format: {NewsItemId}", newsItemId);
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "Invalid NewsItem ID format" }, ct);
            return badRequest;
        }

        _logger.LogInformation("Fetching video for NewsItem {NewsItemId}", guid);

        var result = await _mediator.Send(new GetVideoByNewsItemIdQuery(guid), ct);

        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to fetch video: {Error}", result.ErrorMessage);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = result.ErrorMessage }, ct);
            return errorResponse;
        }

        if (result.Data == null)
        {
            _logger.LogInformation("No video found for NewsItem {NewsItemId}", guid);
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteAsJsonAsync(new { message = "No video found for this news item" }, ct);
            return notFound;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result.Data, ct);
        return response;
    }

    /// <summary>
    /// GET /api/v1/videos/status/{status}
    /// Get all videos with a specific status (Draft, VideoGenerated, AIValidated, Published, Rejected, Failed).
    /// </summary>
    [Function("GetVideosByStatus")]
    public async Task<HttpResponseData> GetVideosByStatus(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/videos/status/{status}")] HttpRequestData req,
        string status,
        CancellationToken ct)
    {
        if (!Enum.TryParse<VideoStatusEnum>(status, ignoreCase: true, out var statusEnum))
        {
            _logger.LogWarning("Invalid video status: {Status}", status);
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new
            {
                error = "Invalid status",
                validStatuses = Enum.GetNames<VideoStatusEnum>()
            }, ct);
            return badRequest;
        }

        _logger.LogInformation("Fetching videos with status {Status}", statusEnum);

        var result = await _mediator.Send(new GetVideosByStatusQuery(statusEnum), ct);

        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to fetch videos by status: {Error}", result.ErrorMessage);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = result.ErrorMessage }, ct);
            return errorResponse;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            status = statusEnum.ToString(),
            count = result.Data?.Count() ?? 0,
            videos = result.Data
        }, ct);
        return response;
    }
}
