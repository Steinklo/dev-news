using DevNews.Application.Common.Repositories;
using DevNews.Application.NewsItem.Dtos;
using DevNews.Domain.Common;
using Mediator;

namespace DevNews.Application.NewsItem.Queries;

public record GetNewsByIdQuery(Guid Id) : IRequest<ResultResponse<NewsItemDto?>>;

public class GetNewsByIdHandler(INewsItemRepository repository)
    : IRequestHandler<GetNewsByIdQuery, ResultResponse<NewsItemDto?>>
{
    public async ValueTask<ResultResponse<NewsItemDto?>> Handle(
        GetNewsByIdQuery request,
        CancellationToken cancellationToken)
    {
        var result = await repository.GetByIdAsync(request.Id, cancellationToken);

        if (!result.IsSuccess)
            return ResultResponse<NewsItemDto?>.Failure(result.ErrorMessage);

        if (result.Data == null)
            return ResultResponse<NewsItemDto?>.Success(null);

        return ResultResponse<NewsItemDto?>.Success(NewsItemDto.FromDomain(result.Data));
    }
}
