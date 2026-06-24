using System.Threading.RateLimiting;
using DevNews.Functions.NewsApi;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace DevNews.Functions.Middleware;

/// <summary>
/// Per-IP fixed-window rate limiting for the anonymous public news endpoints.
/// Caps wallet exposure (Cosmos RU + Flex Consumption execution) from anonymous floods.
/// The limiter is in-memory, so the limit is enforced per worker instance, not globally.
/// </summary>
public class RateLimitingMiddleware(ILogger<RateLimitingMiddleware> logger) : IFunctionsWorkerMiddleware
{
    private const int PermitsPerWindow = 60;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    // Only the anonymous, key-less GET endpoints are limited. Function-key endpoints pass through.
    private static readonly HashSet<string> RateLimitedFunctions =
    [
        nameof(NewsEndpoints.GetNewsById),
        nameof(NewsEndpoints.GetNewsByCategory),
        nameof(NewsEndpoints.GetCategories),
    ];

    private static readonly PartitionedRateLimiter<string> Limiter =
        PartitionedRateLimiter.Create<string, string>(ip =>
            RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = PermitsPerWindow,
                Window = Window,
                QueueLimit = 0,
                AutoReplenishment = true,
            }));

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();

        // Non-HTTP triggers (timer, orchestrator, activity) and non-limited functions pass straight through.
        if (httpContext is null || !RateLimitedFunctions.Contains(context.FunctionDefinition.Name))
        {
            await next(context);
            return;
        }

        var clientIp = GetClientIp(httpContext);
        using var lease = Limiter.AttemptAcquire(clientIp);

        if (lease.IsAcquired)
        {
            await next(context);
            return;
        }

        logger.LogWarning("Rate limit exceeded for {ClientIp} on {Function}", clientIp, context.FunctionDefinition.Name);

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            httpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        }

        await httpContext.Response.WriteAsJsonAsync(
            new { error = "Rate limit exceeded. Try again later." },
            context.CancellationToken);
    }

    /// <summary>
    /// Resolves the caller IP from X-Forwarded-For (set by the Azure platform in front of the worker),
    /// falling back to the transport remote address. Best-effort: shared NATs and spoofed headers are not defended.
    /// </summary>
    private static string GetClientIp(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var first = forwarded.Split(',')[0].Trim();

            // Azure appends :port to IPv4 entries (e.g. 1.2.3.4:56789) — strip it so the partition is the bare IP.
            var lastColon = first.LastIndexOf(':');
            if (lastColon > 0 && first.IndexOf(':') == lastColon)
            {
                first = first[..lastColon];
            }

            if (!string.IsNullOrEmpty(first))
            {
                return first;
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
