using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;

namespace DevNews.Functions.Common;

internal static class OrchestrationStatusHelper
{
    public static async Task<HttpResponseData> GetStatusResponse<TResult>(
        HttpRequestData req,
        DurableTaskClient client,
        string instanceId,
        CancellationToken cancellationToken) where TResult : class
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
                ? metadata.ReadOutputAs<TResult>()
                : null
        }, cancellationToken);

        return response;
    }
}
