using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace DevNews.Functions.DailyVideo;

public class Triggers
{
    private readonly ILogger<Triggers> _logger;

    public Triggers(ILogger<Triggers> logger)
    {
        _logger = logger;
    }

    [Function(nameof(StartDailyVideo))]
    public async Task<HttpResponseData> StartDailyVideo(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/daily-video/start")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Manual daily video generation triggered");

        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(Orchestrator.DailyVideoOrchestrator));

        _logger.LogInformation("Started daily video orchestration with instance ID: {InstanceId}", instanceId);

        var response = req.CreateResponse(System.Net.HttpStatusCode.Accepted);
        await response.WriteAsJsonAsync(new
        {
            instance_id = instanceId,
            status_url = $"/api/v1/daily-video/status/{instanceId}",
            message = "Daily video generation started"
        }, cancellationToken);

        return response;
    }

    [Function(nameof(GetDailyVideoStatus))]
    public async Task<HttpResponseData> GetDailyVideoStatus(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/daily-video/status/{instanceId}")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string instanceId,
        CancellationToken cancellationToken)
    {
        return await Common.OrchestrationStatusHelper.GetStatusResponse<DailyVideoResult>(
            req, client, instanceId, cancellationToken);
    }
}
