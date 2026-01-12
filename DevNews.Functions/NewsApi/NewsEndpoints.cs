using System.Net;
using DevNews.Application.Common.Repositories;
using DevNews.Domain.NewsItem.Enums;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DevNews.Functions.NewsApi;

/// <summary>
/// HTTP API endpoints for querying stored news items.
/// </summary>
public class NewsEndpoints
{
    private readonly INewsItemRepository _repository;
    private readonly ILogger<NewsEndpoints> _logger;

    public NewsEndpoints(INewsItemRepository repository, ILogger<NewsEndpoints> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/news - List recent news items with optional pagination
    /// Query params: limit (default 50, max 100)
    /// </summary>
    [Function(nameof(GetRecentNews))]
    public async Task<HttpResponseData> GetRecentNews(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "news")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var limitStr = query["limit"];
        var limit = 50;

        if (int.TryParse(limitStr, out var parsedLimit))
        {
            limit = Math.Clamp(parsedLimit, 1, 100);
        }

        _logger.LogInformation("Fetching {Limit} recent news items", limit);

        var result = await _repository.GetRecentAsync(limit, cancellationToken);

        if (!result.IsSuccess)
        {
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = result.ErrorMessage }, cancellationToken);
            return errorResponse;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            count = result.Data!.Count(),
            items = result.Data!.Select(MapToDto)
        }, cancellationToken);

        return response;
    }

    /// <summary>
    /// GET /api/news/{id} - Get a single news item by ID
    /// </summary>
    [Function(nameof(GetNewsById))]
    public async Task<HttpResponseData> GetNewsById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "news/{id}")] HttpRequestData req,
        string id,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var guid))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "Invalid ID format" }, cancellationToken);
            return badRequest;
        }

        _logger.LogInformation("Fetching news item {Id}", guid);

        var result = await _repository.GetByIdAsync(guid, cancellationToken);

        if (!result.IsSuccess || result.Data == null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteAsJsonAsync(new { error = $"News item {id} not found" }, cancellationToken);
            return notFound;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(MapToDto(result.Data), cancellationToken);

        return response;
    }

    /// <summary>
    /// GET /api/news/category/{category} - Get news items by category
    /// Query params: limit (default 50, max 100), days (default 30)
    /// </summary>
    [Function(nameof(GetNewsByCategory))]
    public async Task<HttpResponseData> GetNewsByCategory(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "news/category/{category}")] HttpRequestData req,
        string category,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<CategoryEnum>(category, ignoreCase: true, out var categoryEnum))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new
            {
                error = $"Invalid category '{category}'",
                validCategories = Enum.GetNames<CategoryEnum>()
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

        var daysStr = query["days"];
        var days = 30;
        if (int.TryParse(daysStr, out var parsedDays))
        {
            days = Math.Clamp(parsedDays, 1, 365);
        }

        var endDate = DateTimeOffset.UtcNow;
        var startDate = endDate.AddDays(-days);

        _logger.LogInformation(
            "Fetching news for category {Category}, last {Days} days, limit {Limit}",
            categoryEnum, days, limit);

        var result = await _repository.GetByCategoryAndDateRangeAsync(
            categoryEnum, startDate, endDate, cancellationToken);

        if (!result.IsSuccess)
        {
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = result.ErrorMessage }, cancellationToken);
            return errorResponse;
        }

        var items = result.Data!
            .OrderByDescending(n => n.PublishedAt ?? n.CreatedAt)
            .Take(limit);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            category = categoryEnum.ToString(),
            count = items.Count(),
            items = items.Select(MapToDto)
        }, cancellationToken);

        return response;
    }

    /// <summary>
    /// GET /api/news/search - Search news items by URL
    /// Query params: url (required)
    /// </summary>
    [Function(nameof(SearchNewsByUrl))]
    public async Task<HttpResponseData> SearchNewsByUrl(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "news/search")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var url = query["url"];

        if (string.IsNullOrWhiteSpace(url))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "Query parameter 'url' is required" }, cancellationToken);
            return badRequest;
        }

        _logger.LogInformation("Searching for news by URL: {Url}", url);

        var result = await _repository.GetByUrlAsync(url, cancellationToken);

        if (!result.IsSuccess || result.Data == null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteAsJsonAsync(new { error = "No news item found with that URL" }, cancellationToken);
            return notFound;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(MapToDto(result.Data), cancellationToken);

        return response;
    }

    /// <summary>
    /// GET /api/news/categories - List all available categories
    /// </summary>
    [Function(nameof(GetCategories))]
    public HttpResponseData GetCategories(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "news/categories")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.WriteAsJsonAsync(new
        {
            categories = Enum.GetValues<CategoryEnum>().Select(c => new
            {
                id = (int)c,
                name = c.ToString()
            })
        });

        return response;
    }

    private static NewsItemDto MapToDto(DevNews.Domain.NewsItem.NewsItem item)
    {
        return new NewsItemDto
        {
            Id = item.Id.ToString(),
            Title = item.Title.Value,
            Summary = item.Summary.Value,
            Url = item.Url.Value,
            Category = item.Category.Value.ToString(),
            RelevanceScore = item.RelevanceScore.Value,
            Severity = item.Severity?.ToString(),
            Tags = item.Tags.ToList(),
            PublishedAt = item.PublishedAt,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }
}

/// <summary>
/// DTO for API responses matching CLAUDE.md JSON schema
/// </summary>
public class NewsItemDto
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Url { get; init; }
    public required string Summary { get; init; }
    public required string Category { get; init; }
    public required int RelevanceScore { get; init; }
    public string? Severity { get; init; }
    public List<string> Tags { get; init; } = new();
    public DateTimeOffset? PublishedAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}
