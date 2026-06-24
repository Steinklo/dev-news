using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace DevNews.Functions.SocialPostGeneration;

public class Triggers
{
    private readonly ILogger<Triggers> _logger;

    public Triggers(ILogger<Triggers> logger)
    {
        _logger = logger;
    }

    [Function(nameof(StartSocialPostGeneration))]
    public async Task<HttpResponseData> StartSocialPostGeneration(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/social-posts/start")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Manual social post generation triggered");

        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(Orchestrator.SocialPostOrchestrator));

        _logger.LogInformation("Started social post orchestration with instance ID: {InstanceId}", instanceId);

        var response = req.CreateResponse(System.Net.HttpStatusCode.Accepted);
        await response.WriteAsJsonAsync(new
        {
            instance_id = instanceId,
            status_url = $"/api/v1/social-posts/status/{instanceId}",
            message = "Social post generation started"
        }, cancellationToken);

        return response;
    }

    [Function(nameof(GetSocialPostGenerationStatus))]
    public async Task<HttpResponseData> GetSocialPostGenerationStatus(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/social-posts/status/{instanceId}")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string instanceId,
        CancellationToken cancellationToken)
    {
        return await Common.OrchestrationStatusHelper.GetStatusResponse<SocialPostGenerationResult>(
            req, client, instanceId, cancellationToken);
    }
}
