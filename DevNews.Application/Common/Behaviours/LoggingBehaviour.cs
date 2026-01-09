using Mediator;
using Microsoft.Extensions.Logging;

namespace DevNews.Application.Common.Behaviours;

public sealed class LoggingBehaviour<TRequest, TResponse>(ILogger<TRequest> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IMessage
{
    public async ValueTask<TResponse> Handle(TRequest request, MessageHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        logger.LogTrace("Handling request: {Name} {@Request}", requestName, request);
        var response = await next(request, cancellationToken);
        logger.LogTrace("Handled request: {Name} {@Request}", requestName, request);
        return response;
    }
}