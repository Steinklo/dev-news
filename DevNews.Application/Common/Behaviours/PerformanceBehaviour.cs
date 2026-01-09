using System.Diagnostics;
using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.Common.Behaviours;

public sealed class PerformanceBehaviour<TRequest, TResponse>(ILogger<TRequest> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IMessage
{
    public async ValueTask<TResponse> Handle(TRequest request, MessageHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var response = await next(request, cancellationToken);

        stopwatch.Stop();
        var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;

        if (elapsedSeconds > 60)
        {
            var requestName = typeof(TRequest).Name;
            logger.LogWarning("Long-running request: {Name} ({ElapsedSeconds} seconds) {@Request}", requestName, elapsedSeconds, request);
        }

        return response;
    }
}