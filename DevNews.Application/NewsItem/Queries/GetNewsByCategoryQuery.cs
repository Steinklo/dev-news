using DevNews.Application.Common.Repositories;
using DevNews.Application.NewsItem.Dtos;
using DevNews.Domain.Common;
using DevNews.Domain.NewsItem.Enums;
using Mediator;

namespace DevNews.Application.NewsItem.Queries;

public record GetNewsByCategoryQuery(
    CategoryEnum Category,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    int Limit = 50) : IRequest<ResultResponse<NewsListResponseDto>>;

public class GetNewsByCategoryHandler(INewsItemRepository repository)
    : IRequestHandler<GetNewsByCategoryQuery, ResultResponse<NewsListResponseDto>>
{
    public async ValueTask<ResultResponse<NewsListResponseDto>> Handle(
        GetNewsByCategoryQuery request,
        CancellationToken cancellationToken)
    {
        var result = await repository.GetByCategoryAndMonthAsync(
            request.Category,
            request.StartDate,
            request.EndDate,
            request.Limit,
            cancellationToken);

        if (!result.IsSuccess)
            return ResultResponse<NewsListResponseDto>.Failure(result.ErrorMessage);

        var items = result.Data!.Select(NewsItemDto.FromDomain).ToList();

        var response = new NewsListResponseDto(
            Category: request.Category.ToString(),
            YearMonth: request.StartDate.ToString("yyyy-MM"),
            Count: items.Count,
            Items: items);

        return ResultResponse<NewsListResponseDto>.Success(response);
    }
}
