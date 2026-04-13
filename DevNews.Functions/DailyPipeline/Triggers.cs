using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace DevNews.Functions.DailyPipeline;

public class Triggers
{
    private readonly ILogger<Triggers> _logger;

    public Triggers(ILogger<Triggers> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Timer trigger - runs daily at 06:00 UTC
    /// </summary>
    [Function(nameof(DailyPipelineTimer))]
    public async Task DailyPipelineTimer(
        [TimerTrigger("%DailyPipelineSchedule%")] TimerInfo timerInfo,
        [DurableClient] DurableTaskClient client)
    {
        _logger.LogInformation("Daily pipeline timer triggered at {Time}", DateTime.UtcNow);

        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(Orchestrator.DailyPipelineOrchestrator));

        _logger.LogInformation("Started daily pipeline with instance ID: {InstanceId}", instanceId);
    }

    /// <summary>
    /// HTTP trigger - manually start the daily pipeline
    /// </summary>
    [Function(nameof(StartDailyPipeline))]
    public async Task<HttpResponseData> StartDailyPipeline(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/pipeline/start")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Manual daily pipeline triggered");

        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(Orchestrator.DailyPipelineOrchestrator));

        _logger.LogInformation("Started daily pipeline with instance ID: {InstanceId}", instanceId);

        var response = req.CreateResponse(System.Net.HttpStatusCode.Accepted);
        await response.WriteAsJsonAsync(new
        {
            instance_id = instanceId,
            status_url = $"/api/v1/pipeline/status/{instanceId}",
            message = "Daily pipeline started"
        }, cancellationToken);

        return response;
    }

    /// <summary>
    /// HTTP trigger - check status of daily pipeline
    /// </summary>
    [Function(nameof(GetDailyPipelineStatus))]
    public async Task<HttpResponseData> GetDailyPipelineStatus(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/pipeline/status/{instanceId}")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string instanceId,
        CancellationToken cancellationToken)
    {
        return await Common.OrchestrationStatusHelper.GetStatusResponse<DailyPipelineResult>(
            req, client, instanceId, cancellationToken);
    }
}
