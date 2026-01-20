# DevNews.Infrastructure - Infrastructure Layer Guide

This layer implements Application interfaces with concrete technologies (Cosmos DB, Anthropic AI, Azure Blob Storage).

> For project-wide context and global rules, see the root [CLAUDE.md](../CLAUDE.md).

## Folder Structure

```
DevNews.Infrastructure/
  Persistence/        # Cosmos DB document models
    NewsItemDocument.cs
    VideoContentDocument.cs
  Repositories/       # Repository implementations
    NewsItemCosmosRepository.cs
    VideoContentCosmosRepository.cs
  Services/           # AI services, external API integrations
    AnthropicAiService.cs      # Core Claude AI integration
    AiCrawlService.cs          # Article discovery
    AiCurationService.cs       # Content curation
    AiDuplicationService.cs    # Deduplication
    AiVideoScriptGenerator.cs  # Video script generation
    AiVideoValidationService.cs
    VideoBlobStorageService.cs
    VideoGeneratorService.cs
    VideoPublishingService.cs
  ConfigureServices.cs
```

## Core Patterns

### 1. Document Model Pattern (Persistence)

Cosmos DB documents are separate from domain models. Include bidirectional mapping.

```csharp
public class NewsItemDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;  // Cosmos requires lowercase 'id'

    [JsonPropertyName("key")]
    public string Key { get; set; } = null!; // Partition key

    [JsonPropertyName("title")]
    public string Title { get; set; } = null!;

    // ... flat properties (no value objects)

    public static NewsItemDocument FromDomain(NewsItem newsItem)
    {
        // Compute partition key (persistence concern)
        var keyDate = newsItem.PublishedAt ?? newsItem.CreatedAt;
        var partitionKey = $"{newsItem.Category.Value}_{keyDate:yyyy-MM}";

        return new NewsItemDocument
        {
            Id = newsItem.Id.ToString(),
            Key = partitionKey,
            Title = newsItem.Title.Value,  // Extract value from value object
            // ... map all properties
        };
    }

    public NewsItem ToDomain()
    {
        return NewsItem.Reconstitute(  // Use domain's Reconstitute method
            id: Guid.Parse(Id),
            title: Title,
            // ... pass primitive values
        );
    }
}
```

### 2. Repository Pattern

Repositories implement Application interfaces and handle all Cosmos DB operations.

```csharp
public sealed class NewsItemCosmosRepository(
    CosmosClient client,
    string databaseId,
    string containerId) : INewsItemRepository
{
    private readonly Container _container = client.GetContainer(databaseId, containerId);

    public async Task<ResultResponse<NewsItem>> AddAsync(
        NewsItem newsItem,
        CancellationToken ct = default)
    {
        try
        {
            newsItem.ClearDomainEvents();  // Clear events after dispatch
            var document = NewsItemDocument.FromDomain(newsItem);

            var response = await _container.CreateItemAsync(
                document,
                new PartitionKey(document.Key),
                cancellationToken: ct);

            return ResultResponse<NewsItem>.Success(response.Resource.ToDomain());
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            return ResultResponse<NewsItem>.Failure($"NewsItem {newsItem.Id} already exists");
        }
        catch (Exception ex)
        {
            return ResultResponse<NewsItem>.Failure($"Failed to add: {ex.Message}");
        }
    }

    public async Task<ResultResponse<NewsItem?>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        // Query by id (cross-partition if needed)
        var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
            .WithParameter("@id", id.ToString());

        var iterator = _container.GetItemQueryIterator<NewsItemDocument>(query);
        // ...
    }
}
```

### 3. AI Service Pattern

AI services call Anthropic Claude and parse structured responses.

```csharp
public class AiVideoScriptGenerator(
    IAiService aiService,
    ILogger<AiVideoScriptGenerator> logger) : IVideoScriptGenerator
{
    public async Task<ResultResponse<string>> GenerateScriptAsync(
        string title, string summary, string category,
        int targetDurationSeconds, CancellationToken ct = default)
    {
        try
        {
            var prompt = BuildScriptPrompt(title, summary, category, targetDurationSeconds);

            var result = await aiService.GenerateAsync(prompt, ct);

            if (!result.IsSuccess)
                return ResultResponse<string>.Failure(result.ErrorMessage ?? "AI service failed");

            // Parse structured JSON response
            var scriptResponse = JsonSerializer.Deserialize<ScriptResponse>(result.Data!);

            return scriptResponse?.Script != null
                ? ResultResponse<string>.Success(scriptResponse.Script)
                : ResultResponse<string>.Failure("Invalid response format");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error");
            return ResultResponse<string>.Failure($"Failed: {ex.Message}");
        }
    }

    private static string BuildScriptPrompt(...) { /* Build prompt */ }

    private class ScriptResponse { public string? Script { get; set; } }
}
```

### 4. AI Prompt Best Practices

When building AI prompts:

1. **Be specific about output format** - provide JSON schema
2. **Provide examples** of good output
3. **List constraints explicitly** - word counts, requirements
4. **Request "JSON only (no markdown)"** to avoid code blocks in response
5. **Parse safely** - handle null and malformed responses
6. **Log prompts at Debug level** for troubleshooting

```csharp
private static string BuildPrompt(string input)
{
    return $@"You are [role].

[Task description]

**Input**: {input}

**Requirements**:
- Requirement 1
- Requirement 2

**Output Format**:
Return ONLY valid JSON (no markdown formatting):
{{
  ""field"": ""value""
}}

**Example Good Output**:
{{
  ""field"": ""example value""
}}

Now process the input.";
}
```

## Service Registration

All services registered in `ConfigureServices.cs`:

```csharp
public static IServiceCollection AddInfrastructureServices(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // Cosmos DB
    services.AddSingleton<CosmosClient>(_ =>
        new CosmosClient(configuration["CosmosDbEndpoint"], configuration["CosmosDbKey"]));

    // Repositories
    services.AddScoped<INewsItemRepository>(sp =>
    {
        var cosmosClient = sp.GetRequiredService<CosmosClient>();
        return new NewsItemCosmosRepository(cosmosClient, "dev-news-db", "news-items");
    });

    // AI services
    services.AddSingleton<IAiService, AnthropicAiService>();
    services.AddScoped<IVideoScriptGenerator, AiVideoScriptGenerator>();

    // Azure Blob Storage
    services.AddSingleton(sp =>
    {
        var connectionString = configuration["AzureBlobStorage:ConnectionString"];
        return new BlobServiceClient(connectionString);
    });
    services.AddScoped<IVideoBlobStorage, VideoBlobStorageService>();

    return services;
}
```

## Adding a New Repository

1. Create document model in `Persistence/[Aggregate]Document.cs`
2. Implement `FromDomain()` and `ToDomain()` methods
3. Create repository in `Repositories/[Aggregate]CosmosRepository.cs`
4. Implement interface from Application layer
5. Register in `ConfigureServices.cs`
6. Choose appropriate partition key for your access patterns

## Adding a New AI Service

1. Create service in `Services/Ai[Purpose]Service.cs`
2. Inject `IAiService` for Claude API calls
3. Build structured prompts with clear output format
4. Parse JSON responses safely with error handling
5. Return `ResultResponse<T>` for all operations
6. Register in `ConfigureServices.cs`

## Checklist for New Infrastructure Code

- [ ] Document model has `FromDomain`/`ToDomain` methods
- [ ] Repository catches and wraps exceptions in `ResultResponse`
- [ ] AI prompts specify exact output format with examples
- [ ] Services registered in `ConfigureServices.cs`
- [ ] Logging includes context (IDs, operation names)
- [ ] `CancellationToken` passed to all async operations
- [ ] Cosmos partition keys chosen for query efficiency
