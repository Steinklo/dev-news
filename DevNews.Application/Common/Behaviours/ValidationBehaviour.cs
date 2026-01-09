using FluentValidation;
using Mediator;
using ValidationException = DevNews.Application.Common.Exceptions.ValidationException;

namespace DevNews.Application.Common.Behaviours;

public sealed class ValidationBehaviour<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IMessage
{
    public async ValueTask<TResponse> Handle(TRequest request, MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            
            var validationResults = await Task.WhenAll(
                validators.Select(v =>
                    v.ValidateAsync(context, cancellationToken)));
            
            var failures = validationResults
                .Where(r => r.Errors.Any())
                .SelectMany(r => r.Errors)
                .ToList();
            
            if (failures.Any())
            {
                var stringSeparators = failures.Select(x => x.ErrorMessage)
                    .Aggregate((a, b) => $"{a}, {b}");
                throw new ValidationException(stringSeparators);
            }
        }
        
        return await next(request, cancellationToken);
    }
}