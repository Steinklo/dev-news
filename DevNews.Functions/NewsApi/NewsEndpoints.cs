using System.Net;
using DevNews.Application.NewsItem.Dtos;
using DevNews.Application.NewsItem.Queries;
using DevNews.Domain.NewsItem.Enums;
using Mediator;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DevNews.Functions.NewsApi;

/// <summary>
/// HTTP API endpoints for querying stored news items.
/// Uses CQRS pattern - all queries go through Mediator to Application layer.
/// </summary>
public class NewsEndpoints(IMediator mediator, ILogger<NewsEndpoints> logger)
{
    /// <summary>
    /// GET /api/v1/news/{id} - Get a single news item by ID
    /// </summary>
    [Function(nameof(GetNewsById))]
    public async Task<HttpResponseData> GetNewsById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/news/{id}")] HttpRequestData req,
        string id,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var guid))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "Invalid ID format" }, cancellationToken);
            return badRequest;
        }

        logger.LogInformation("Fetching news item {Id}", guid);

        var result = await mediator.Send(new GetNewsByIdQuery(guid), cancellationToken);

        if (!result.IsSuccess)
        {
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = result.ErrorMessage }, cancellationToken);
            return errorResponse;
        }

        if (result.Data == null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteAsJsonAsync(new { error = $"News item {id} not found" }, cancellationToken);
            return notFound;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result.Data, cancellationToken);

        return response;
    }

    /// <summary>
    /// GET /api/v1/news/category/{category} - Get news items by category
    /// Query params: year_month (format: 2026-01, defaults to current), limit (default 50, max 100)
    /// </summary>
    [Function(nameof(GetNewsByCategory))]
    public async Task<HttpResponseData> GetNewsByCategory(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/news/category/{category}")] HttpRequestData req,
        string category,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<CategoryEnum>(category, ignoreCase: true, out var categoryEnum))
        {
            var categoriesResult = await mediator.Send(new GetCategoriesQuery(), cancellationToken);
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new
            {
                error = $"Invalid category '{category}'",
                valid_categories = categoriesResult.Data?.Categories.Select(c => c.Name) ?? []
            }, cancellationToken);
            return badRequest;
        }

        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);

        var limitStr = query["limit"];
        var limit = 50;
        if (int.TryParse(limitStr, out var parsedLimit))
        {
            limit = Math.Clamp(parsedLimit, 1, 100);
        }

        // Parse year_month parameter (format: 2026-01), default to current
        var yearMonthStr = query["year_month"];
        DateTimeOffset startDate;
        DateTimeOffset endDate;

        if (!string.IsNullOrWhiteSpace(yearMonthStr) &&
            DateTime.TryParseExact(yearMonthStr, "yyyy-MM", null, System.Globalization.DateTimeStyles.None, out var parsed))
        {
            startDate = new DateTimeOffset(parsed, TimeSpan.Zero);
            endDate = startDate.AddMonths(1);
        }
        else
        {
            // Default to current month
            var now = DateTimeOffset.UtcNow;
            startDate = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
            endDate = startDate.AddMonths(1);
        }

        logger.LogInformation(
            "Fetching news for category {Category}, year_month {YearMonth}, limit {Limit}",
            categoryEnum, startDate.ToString("yyyy-MM"), limit);

        var result = await mediator.Send(
            new GetNewsByCategoryQuery(categoryEnum, startDate, endDate, limit),
            cancellationToken);

        if (!result.IsSuccess)
        {
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = result.ErrorMessage }, cancellationToken);
            return errorResponse;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result.Data, cancellationToken);

        return response;
    }

    /// <summary>
    /// GET /api/v1/news/categories - List all available categories
    /// </summary>
    [Function(nameof(GetCategories))]
    public async Task<HttpResponseData> GetCategories(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/news/categories")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetCategoriesQuery(), cancellationToken);

        if (!result.IsSuccess)
        {
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = result.ErrorMessage }, cancellationToken);
            return errorResponse;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result.Data, cancellationToken);

        return response;
    }
}
