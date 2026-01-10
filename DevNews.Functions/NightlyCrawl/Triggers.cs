using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace DevNews.Functions.NightlyCrawl;

public class Triggers
{
    private readonly ILogger<Triggers> _logger;

    public Triggers(ILogger<Triggers> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Timer trigger - runs nightly at 2 AM UTC
    /// </summary>
    [Function(nameof(NightlyCrawlTimer))]
    public async Task NightlyCrawlTimer(
        [TimerTrigger("0 0 2 * * *")] TimerInfo timer,
        [DurableClient] DurableTaskClient client,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Nightly crawl timer triggered at {Time}", DateTime.UtcNow);

        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(Orchestrator.NightlyCrawlOrchestrator));

        _logger.LogInformation("Started nightly crawl orchestration with instance ID: {InstanceId}", instanceId);
    }

    /// <summary>
    /// HTTP trigger - manually start the nightly crawl (for testing/ad-hoc runs)
    /// </summary>
    [Function(nameof(StartNightlyCrawl))]
    public async Task<HttpResponseData> StartNightlyCrawl(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "crawl/start")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Manual nightly crawl triggered");

        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(Orchestrator.NightlyCrawlOrchestrator));

        _logger.LogInformation("Started nightly crawl orchestration with instance ID: {InstanceId}", instanceId);

        var response = req.CreateResponse(System.Net.HttpStatusCode.Accepted);
        await response.WriteAsJsonAsync(new
        {
            instanceId,
            statusQueryUrl = $"/crawl/status/{instanceId}",
            message = "Nightly crawl orchestration started"
        }, cancellationToken);

        return response;
    }

    /// <summary>
    /// HTTP trigger - check status of an orchestration
    /// </summary>
    [Function(nameof(GetCrawlStatus))]
    public async Task<HttpResponseData> GetCrawlStatus(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "crawl/status/{instanceId}")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string instanceId,
        CancellationToken cancellationToken)
    {
        var metadata = await client.GetInstanceAsync(instanceId);

        if (metadata == null)
        {
            var notFound = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
            await notFound.WriteAsJsonAsync(new { error = "Instance not found" }, cancellationToken);
            return notFound;
        }

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            instanceId = metadata.InstanceId,
            status = metadata.RuntimeStatus.ToString(),
            createdAt = metadata.CreatedAt,
            lastUpdatedAt = metadata.LastUpdatedAt,
            output = metadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed
                ? metadata.ReadOutputAs<NightlyCrawlResult>()
                : null
        }, cancellationToken);

        return response;
    }
}
