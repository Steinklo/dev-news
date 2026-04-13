using Microsoft.DurableTask;

namespace DevNews.Functions.Common;

internal static class OrchestrationDefaults
{
    public static TaskOptions RetryOptions => TaskOptions.FromRetryPolicy(new RetryPolicy(
        maxNumberOfAttempts: 3,
        firstRetryInterval: TimeSpan.FromSeconds(5),
        backoffCoefficient: 2.0));
}
