using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
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
    /// HTTP trigger - manually start video generation
    /// </summary>
    [Function(nameof(StartVideoGeneration))]
    public async Task<HttpResponseData> StartVideoGeneration(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/video-generation/start")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Manual video generation triggered");

        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(Orchestrator.VideoGenerationOrchestrator));

        _logger.LogInformation("Started video generation orchestration with instance ID: {InstanceId}", instanceId);

        var response = req.CreateResponse(System.Net.HttpStatusCode.Accepted);
        await response.WriteAsJsonAsync(new
        {
            instance_id = instanceId,
            status_url = $"/api/v1/video-generation/status/{instanceId}",
            message = "Video generation orchestration started"
        }, cancellationToken);

        return response;
    }

    /// <summary>
    /// HTTP trigger - check status of video generation orchestration
    /// </summary>
    [Function(nameof(GetVideoGenerationStatus))]
    public async Task<HttpResponseData> GetVideoGenerationStatus(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/video-generation/status/{instanceId}")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string instanceId,
        CancellationToken cancellationToken)
    {
        return await Common.OrchestrationStatusHelper.GetStatusResponse<VideoGenerationResult>(
            req, client, instanceId, cancellationToken);
    }
}
