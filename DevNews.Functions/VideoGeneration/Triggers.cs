using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace DevNews.Functions.VideoGeneration;

public class Triggers
{
    private readonly ILogger<Triggers> _logger;

    public Triggers(ILogger<Triggers> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// HTTP trigger to start video generation for a specific NewsItem.
    /// POST /api/v1/video/generate/{newsItemId}
    /// </summary>
    [Function(nameof(StartVideoGeneration))]
    public async Task<HttpResponseData> StartVideoGeneration(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/video/generate/{newsItemId}")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string newsItemId,
        CancellationToken ct)
    {
        if (!Guid.TryParse(newsItemId, out var guid))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "Invalid NewsItem ID format" }, ct);
            return badRequest;
        }

        _logger.LogInformation("Starting video generation orchestration for NewsItem {NewsItemId}", guid);

        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(Orchestrator.GenerateVideoOrchestration),
            guid);

        _logger.LogInformation("Video generation orchestration started: {InstanceId}", instanceId);

        var response = req.CreateResponse(HttpStatusCode.Accepted);
        await response.WriteAsJsonAsync(new
        {
            instanceId,
            newsItemId = guid.ToString(),
            statusQueryGetUri = $"/api/v1/video/status/{instanceId}",
            message = "Video generation started"
        }, ct);

        return response;
    }

    /// <summary>
    /// HTTP trigger to check the status of a video generation orchestration.
    /// GET /api/v1/video/status/{instanceId}
    /// </summary>
    [Function(nameof(GetVideoGenerationStatus))]
    public async Task<HttpResponseData> GetVideoGenerationStatus(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/video/status/{instanceId}")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string instanceId,
        CancellationToken ct)
    {
        _logger.LogInformation("Checking status for orchestration {InstanceId}", instanceId);

        var metadata = await client.GetInstanceAsync(instanceId, getInputsAndOutputs: true);

        if (metadata == null)
        {
            _logger.LogWarning("Orchestration {InstanceId} not found", instanceId);
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteAsJsonAsync(new { error = "Orchestration instance not found" }, ct);
            return notFound;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);

        var result = new
        {
            instanceId = metadata.InstanceId,
            runtimeStatus = metadata.RuntimeStatus.ToString(),
            createdTime = metadata.CreatedAt,
            lastUpdatedTime = metadata.LastUpdatedAt,
            input = metadata.SerializedInput != null ? System.Text.Json.JsonSerializer.Deserialize<Guid>(metadata.SerializedInput) : (Guid?)null,
            output = metadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed && metadata.SerializedOutput != null
                ? System.Text.Json.JsonSerializer.Deserialize<VideoGenerationResult>(metadata.SerializedOutput)
                : null
        };

        await response.WriteAsJsonAsync(result, ct);
        return response;
    }
}
