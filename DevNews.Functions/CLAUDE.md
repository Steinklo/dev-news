# DevNews.Functions - Azure Functions Layer Guide

This layer provides HTTP endpoints and Durable Functions orchestration.

> For project-wide context and global rules, see the root [CLAUDE.md](../CLAUDE.md).

## Folder Structure

```
DevNews.Functions/
  NewsApi/            # HTTP endpoints for news queries
    NewsEndpoints.cs
  VideoApi/           # HTTP endpoints for video queries
    VideoEndpoints.cs
  NightlyCrawl/       # Durable orchestration for article discovery
    Orchestrator.cs   # Main orchestration logic
    Activities.cs     # Individual activity functions
    Triggers.cs       # Timer trigger to start orchestration
    Dtos.cs           # DTOs for orchestration data
  VideoGeneration/    # Durable orchestration for video generation
    Orchestrator.cs
    Activities.cs
    Triggers.cs
    Dtos.cs
  Properties/
    launchSettings.json
  Program.cs          # Entry point and DI configuration
  host.json
```

## Core Patterns

### 1. HTTP Endpoint Pattern

HTTP endpoints use MediatR to delegate to Application layer.

```csharp
public class NewsEndpoints(IMediator mediator, ILogger<NewsEndpoints> logger)
{
    [Function(nameof(GetNewsById))]
    public async Task<HttpResponseData> GetNewsById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/news/{id}")] HttpRequestData req,
        string id,
        CancellationToken cancellationToken)
    {
        // 1. Validate input
        if (!Guid.TryParse(id, out var guid))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "Invalid ID format" }, cancellationToken);
            return badRequest;
        }

        // 2. Send query via MediatR
        var result = await mediator.Send(new GetNewsByIdQuery(guid), cancellationToken);

        // 3. Handle result
        if (!result.IsSuccess)
        {
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteAsJsonAsync(new { error = result.ErrorMessage }, cancellationToken);
            return error;
        }

        if (result.Data == null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteAsJsonAsync(new { error = "Not found" }, cancellationToken);
            return notFound;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result.Data, cancellationToken);
        return response;
    }
}
```

### 2. Durable Functions Orchestrator Pattern

Orchestrators coordinate long-running workflows with retry logic.

```csharp
public class Orchestrator
{
    private static TaskOptions CreateRetryOptions()
    {
        return TaskOptions.FromRetryPolicy(new RetryPolicy(
            maxNumberOfAttempts: 2,
            firstRetryInterval: TimeSpan.FromSeconds(5),
            backoffCoefficient: 2.0));
    }

    [Function(nameof(GenerateVideoOrchestration))]
    public async Task<VideoGenerationResult> GenerateVideoOrchestration(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger<Orchestrator>();
        var newsItemId = context.GetInput<Guid>();

        try
        {
            // Stage 1: Generate script
            var scriptResult = await context.CallActivityAsync<GenerateScriptResult>(
                nameof(Activities.GenerateScriptActivity),
                newsItemId,
                CreateRetryOptions());

            if (!scriptResult.Success)
                return new VideoGenerationResult(Success: false, Stage: "ScriptGeneration", Error: scriptResult.Error);

            // Stage 2: Generate video file
            var videoResult = await context.CallActivityAsync<GenerateVideoResult>(
                nameof(Activities.GenerateVideoActivity),
                scriptResult,
                CreateRetryOptions());

            // ... more stages

            // Fan-out: Parallel publishing to multiple platforms
            var publishTasks = platforms.Select(platform =>
                context.CallActivityAsync<PlatformPublishResult>(
                    nameof(Activities.PublishToPlatformActivity),
                    new PublishRequest(...),
                    CreateRetryOptions()));

            var publishResults = await Task.WhenAll(publishTasks);

            return new VideoGenerationResult(Success: true, Stage: "Completed");
        }
        catch (Exception ex)
        {
            return new VideoGenerationResult(Success: false, Error: ex.Message);
        }
    }
}
```

### 3. Activity Pattern

Activities are individual units of work called by orchestrators.

```csharp
public class Activities(IMediator mediator, ILogger<Activities> logger)
{
    [Function(nameof(GenerateScriptActivity))]
    public async Task<GenerateScriptResult> GenerateScriptActivity(
        [ActivityTrigger] Guid newsItemId,
        CancellationToken ct)
    {
        try
        {
            logger.LogInformation("Activity: Generating script for {NewsItemId}", newsItemId);

            var result = await mediator.Send(new GenerateVideoCommand(newsItemId), ct);

            return result.IsSuccess
                ? new GenerateScriptResult(true, result.Data, null, null)
                : new GenerateScriptResult(false, null, null, result.ErrorMessage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Activity failed");
            return new GenerateScriptResult(false, null, null, ex.Message);
        }
    }
}
```

### 4. Orchestration DTOs

Use records for orchestration data transfer.

```csharp
public record GenerateScriptResult(
    bool Success,
    Guid? VideoContentId,
    string? Script,
    string? Error);

public record VideoGenerationResult(
    bool Success,
    string Stage,
    string? Error = null,
    int PublishedCount = 0);

public record PublishRequest(
    Guid VideoContentId,
    PublishPlatformEnum Platform,
    string VideoUrl);
```

### 5. Rate Limiting in Orchestrations

For AI API rate limits, use timers between sequential calls:

```csharp
foreach (var article in articles)
{
    var result = await context.CallActivityAsync<Result>(
        nameof(Activities.CurateActivity),
        article,
        CreateRetryOptions());

    // Rate limit: 50 requests/min = wait 2s between calls
    await context.CreateTimer(
        context.CurrentUtcDateTime.AddSeconds(2),
        CancellationToken.None);
}
```

## Adding a New HTTP Endpoint

1. Create file in appropriate API folder (`NewsApi/`, `VideoApi/`, etc.)
2. Inject `IMediator` and `ILogger<T>` via constructor
3. Use `[Function]` attribute with unique name (nameof recommended)
4. Use `[HttpTrigger]` with authorization level and route pattern
5. Validate input before sending to MediatR
6. Handle all result cases (success, failure, not found)

## Adding a New Orchestration

1. Create folder: `DevNews.Functions/[Feature]/`
2. Create files:
   - `Orchestrator.cs` - Main workflow logic
   - `Activities.cs` - Individual steps
   - `Triggers.cs` - HTTP/Timer triggers to start
   - `Dtos.cs` - Data transfer records
3. Use retry policies for resilience
4. Use fan-out/fan-in for parallel operations
5. Use `CreateReplaySafeLogger` in orchestrators

## Checklist for New Functions Code

- [ ] HTTP endpoints validate input before MediatR calls
- [ ] Orchestrators use `CreateReplaySafeLogger`
- [ ] Activities are idempotent (safe to retry)
- [ ] DTOs are records (immutable, serializable)
- [ ] Rate limiting applied for external API calls
- [ ] Proper error handling at each stage
- [ ] Unique function names (use `nameof`)
- [ ] Appropriate authorization level (Anonymous vs Function)
