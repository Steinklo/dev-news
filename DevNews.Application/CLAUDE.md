# DevNews.Application - Application Layer Guide

This layer orchestrates use cases using CQRS pattern (Commands & Queries) with MediatR.

> For project-wide context and global rules, see the root [CLAUDE.md](../CLAUDE.md).

## Folder Structure

```
DevNews.Application/
  Common/
    Behaviours/       # Pipeline behaviors (validation, logging, performance, exceptions)
    Exceptions/       # ValidationException, NotFoundException
    Models/           # Shared models (CleanedArticle, CrawledArticle)
    Repositories/     # Repository interfaces (INewsItemRepository, IVideoContentRepository)
    Services/         # Service interfaces (IAiService, ICrawlService, IVideoScriptGenerator, etc.)
  NewsItem/
    Commands/         # Write operations (CurateArticleCommand, PersistNewsItemCommand, etc.)
    Queries/          # Read operations (GetNewsByCategoryQuery, GetNewsByIdQuery, etc.)
    Dtos/             # Data transfer objects (NewsItemDto, NewsListResponseDto)
  VideoContent/
    Commands/         # Video commands (GenerateVideoCommand, ValidateVideoCommand, etc.)
    Queries/          # Video queries (GetVideoByNewsItemIdQuery, GetVideosByStatusQuery)
    Dtos/             # VideoContentDto
  ConfigureServices.cs
```

## Core Patterns

### 1. Command Pattern (Write Operations)

Commands modify state and return `ResultResponse<T>`. Use record types for immutability.

```csharp
// Command definition - record for immutability
public record CurateArticleCommand(CrawledArticle Article) : IRequest<ResultResponse<CleanedArticle>>;

// Handler - orchestrates domain + infrastructure
public class CurateArticleHandler(
    ICurationService curationService,
    ILogger<CurateArticleHandler> logger) : IRequestHandler<CurateArticleCommand, ResultResponse<CleanedArticle>>
{
    public async ValueTask<ResultResponse<CleanedArticle>> Handle(
        CurateArticleCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Curating article: {Url}", request.Article.Url);

        var result = await curationService.CurateAsync(request.Article, cancellationToken);

        if (!result.IsSuccess)
        {
            logger.LogWarning("Curation failed: {Error}", result.ErrorMessage);
            return result;
        }

        logger.LogInformation("Successfully curated: {Title}", result.Data!.Title);
        return result;
    }
}
```

### 2. Query Pattern (Read Operations)

Queries return data without side effects.

```csharp
public record GetNewsByCategoryQuery(
    CategoryEnum Category,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    int Limit) : IRequest<ResultResponse<NewsListResponseDto>>;

public class GetNewsByCategoryHandler(
    INewsItemRepository repository,
    ILogger<GetNewsByCategoryHandler> logger) : IRequestHandler<GetNewsByCategoryQuery, ResultResponse<NewsListResponseDto>>
{
    public async ValueTask<ResultResponse<NewsListResponseDto>> Handle(
        GetNewsByCategoryQuery request,
        CancellationToken ct)
    {
        var items = await repository.GetByCategoryAsync(
            request.Category, request.StartDate, request.EndDate, request.Limit, ct);

        var dtos = items.Select(NewsItemDto.FromDomain).ToList();
        return ResultResponse<NewsListResponseDto>.Success(new NewsListResponseDto(dtos));
    }
}
```

### 3. DTO Pattern

DTOs are records with JSON attributes for API serialization. Include static `FromDomain()` method.

```csharp
public record VideoContentDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("newsItemId")] string NewsItemId,
    [property: JsonPropertyName("script")] string Script,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("videoUrl")] string? VideoUrl,
    [property: JsonPropertyName("publishResults")] IReadOnlyList<PublishResultDto> PublishResults)
{
    public static VideoContentDto FromDomain(Domain.VideoContent.VideoContent videoContent)
    {
        return new VideoContentDto(
            Id: videoContent.Id.ToString(),
            NewsItemId: videoContent.NewsItemId.ToString(),
            Script: videoContent.Script.Value,
            Status: videoContent.Status.ToString(),
            VideoUrl: videoContent.BlobStorageUrl,
            PublishResults: videoContent.PublishResults
                .Select(PublishResultDto.FromDomain)
                .ToList());
    }
}
```

### 4. Service Interfaces

Define interfaces for external dependencies. Infrastructure layer implements these.

```csharp
public interface IVideoScriptGenerator
{
    Task<ResultResponse<string>> GenerateScriptAsync(
        string title,
        string summary,
        string category,
        int targetDurationSeconds,
        CancellationToken ct = default);
}

public interface IVideoValidationService
{
    Task<ResultResponse<(bool IsValid, string Reason)>> ValidateVideoAsync(
        string videoUrl,
        string originalScript,
        CancellationToken ct = default);
}
```

### 5. Pipeline Behaviors

Cross-cutting concerns registered in `ConfigureServices.cs`:

```csharp
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehaviour<,>));
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(PerformanceBehaviour<,>));
```

## Adding a New Command

1. Create file: `[Feature]/Commands/[Action]Command.cs`
2. Define record: `public record MyCommand(params) : IRequest<ResultResponse<T>>;`
3. Create handler class in same file: `public class MyHandler : IRequestHandler<MyCommand, ResultResponse<T>>`
4. Use constructor injection for dependencies (services, repositories)
5. Return `ResultResponse<T>` - never throw for business logic failures
6. Add logging: Debug for entry, Warning for failures, Information for success

## Adding a New Query

1. Create file: `[Feature]/Queries/Get[What]Query.cs`
2. Define record with query parameters
3. Create handler that calls repository and maps to DTOs
4. Queries should be read-only (no side effects)

## Adding a New Service Interface

1. Create interface in `Common/Services/I[Name]Service.cs`
2. Define async methods with `CancellationToken` parameter
3. Return `ResultResponse<T>` for operations that can fail
4. Implementation goes in Infrastructure layer

## Checklist for New Application Code

- [ ] Commands/Queries are records (immutable)
- [ ] Handler returns `ResultResponse<T>`, never throws for business logic
- [ ] Uses interfaces for external dependencies (not concrete classes)
- [ ] Proper logging (Debug entry, Warning failures, Information success)
- [ ] `CancellationToken` propagated to all async calls
- [ ] DTOs have `FromDomain` static method
- [ ] Handlers are thin - delegate to domain/services
