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
    /// Timer trigger - runs daily at 07:00 UTC (1 hour after nightly crawl)
    /// </summary>
    [Function(nameof(DailyVideoGenerationTimer))]
    public async Task DailyVideoGenerationTimer(
        [TimerTrigger("0 0 7 * * *")] TimerInfo timerInfo,
        [DurableClient] DurableTaskClient client)
    {
        _logger.LogInformation("Daily video generation timer triggered at {Time}", DateTime.UtcNow);

        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(Orchestrator.VideoGenerationOrchestrator));

        _logger.LogInformation("Started video generation orchestration with instance ID: {InstanceId}", instanceId);
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
        var metadata = await client.GetInstanceAsync(instanceId, getInputsAndOutputs: true);

        if (metadata == null)
        {
            var notFound = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
            await notFound.WriteAsJsonAsync(new { error = "Instance not found" }, cancellationToken);
            return notFound;
        }

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            instance_id = metadata.InstanceId,
            status = metadata.RuntimeStatus.ToString(),
            created_at = metadata.CreatedAt,
            last_updated_at = metadata.LastUpdatedAt,
            output = metadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed
                ? metadata.ReadOutputAs<VideoGenerationResult>()
                : null
        }, cancellationToken);

        return response;
    }
}
