using DevNews.Application.NewsItem.Dtos;
using DevNews.Domain.Common;
using DevNews.Domain.NewsItem.Enums;
using Mediator;

namespace DevNews.Application.NewsItem.Queries;

public record GetCategoriesQuery : IRequest<ResultResponse<CategoriesResponseDto>>;

public class GetCategoriesHandler : IRequestHandler<GetCategoriesQuery, ResultResponse<CategoriesResponseDto>>
{
    private static readonly CategoriesResponseDto CachedResponse = new(
        Enum.GetValues<CategoryEnum>()
            .Select(c => new CategoryDto((int)c, c.ToString()))
            .ToList()
            .AsReadOnly());

    public ValueTask<ResultResponse<CategoriesResponseDto>> Handle(
        GetCategoriesQuery request,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(ResultResponse<CategoriesResponseDto>.Success(CachedResponse));
    }
}
