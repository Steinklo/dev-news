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
    /// HTTP trigger - manually start the crawl
    /// </summary>
    [Function(nameof(StartNightlyCrawl))]
    public async Task<HttpResponseData> StartNightlyCrawl(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/crawl/start")] HttpRequestData req,
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
            instance_id = instanceId,
            status_url = $"/api/v1/crawl/status/{instanceId}",
            message = "Nightly crawl orchestration started"
        }, cancellationToken);

        return response;
    }

    /// <summary>
    /// HTTP trigger - check status of an orchestration
    /// </summary>
    [Function(nameof(GetCrawlStatus))]
    public async Task<HttpResponseData> GetCrawlStatus(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/crawl/status/{instanceId}")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string instanceId,
        CancellationToken cancellationToken)
    {
        return await Common.OrchestrationStatusHelper.GetStatusResponse<NightlyCrawlResult>(
            req, client, instanceId, cancellationToken);
    }
}
