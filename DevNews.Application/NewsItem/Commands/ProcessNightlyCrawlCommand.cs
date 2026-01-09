using DevNews.Domain.NewsItem.Enums;
using Mediator;

namespace DevNews.Application.NewsItem.Commands;

public record ProcessNightlyCrawlCommand(CategoryEnum Category) : IRequest<Unit>;

public class ProcessNightlyCrawlHandler : IRequestHandler<ProcessNightlyCrawlCommand, Unit>
{
    public ProcessNightlyCrawlHandler(
    )
    {
    }

    public async ValueTask<Unit> Handle(ProcessNightlyCrawlCommand request, CancellationToken cancellationToken)
    {
        // Your entire nightly pipeline here


        return Unit.Value; 
    }
}