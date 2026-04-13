# DevNews Backend

Serverless C# backend for an AI developer news aggregator and short video generator. Azure Functions V4 (.NET 9 isolated worker) with Cosmos DB and Anthropic Claude AI.

## Commands

```bash
dotnet restore DevNews.sln                                    # Restore
dotnet build DevNews.sln --configuration Release              # Build
dotnet test DevNews.UnitTests/DevNews.UnitTests.csproj        # Test
cd DevNews.Functions && func start                            # Run locally
```

## Project Structure

```
DevNews.Domain/           # Entities, value objects, enums, domain events (depends only on Mediator.Abstractions)
DevNews.Application/      # Commands, queries, validators, service interfaces (Mediator CQRS)
DevNews.Infrastructure/   # Cosmos DB repos, Anthropic AI services, RSS crawling, video generation
DevNews.Functions/        # Azure Functions entry point (HTTP endpoints + Durable Functions)
DevNews.UnitTests/        # xUnit tests
```

**Dependency flow**: Functions → Application + Infrastructure → Domain

## Key Patterns

- **Clean Architecture / DDD**: Domain has no external dependencies; Application defines interfaces; Infrastructure implements them
- **CQRS via Mediator**: Source-generated Mediator for commands and queries (not MediatR)
- **Pipeline behaviours**: Logging, validation (FluentValidation), performance tracking, exception handling
- **Value objects**: NewsTitle, NewsUrl, NewsSummary, NewsCategory, RelevanceScore — enforce invariants at domain level
- **Durable Functions orchestrators**: Nightly crawl pipeline (discover → curate → filter low relevance → deduplicate → persist), short video generation pipeline

## API Endpoints

- `GET /api/v1/news/categories` → `CategoriesResponse`
- `GET /api/v1/news/category/{category}?year_month=YYYY-MM` → `NewsByCategoryResponse`
- `GET /api/v1/news/{id}` → single `NewsItem` by GUID

## Infrastructure Services

- `AnthropicAiService` — Claude API wrapper
- `AiCrawlService` — RSS feed discovery + article extraction (SmartReader)
- `AiCurationService` — AI-generated summaries, categories, relevance scores
- `AIDuplicationService` — AI-powered deduplication
- `NewsItemCosmosRepository` — Cosmos DB persistence
- `AiVideoScriptService` — AI-generated video scripts
- `AiVideoScriptValidationService` — script validation
- `CreatomateVideoGenerationService` — video rendering via Creatomate
- `AzureBlobVideoStorageService` — video storage in Azure Blob
- `YouTubePublishingService` — publish to YouTube
- `LinkedInPublishingService` — publish to LinkedIn
- `PlatformPublishingRouter` — routes publishing to correct platform
- `ShortVideoCosmosRepository` — Cosmos DB persistence for videos

## Conventions

- JSON serialization: camelCase, null values omitted
- Nullable reference types enabled throughout
- Categories are a fixed enum (7 values, AI-focused, ordered by priority): AiModelsAndApis, AiDeveloperTools, AgentsAndFrameworks, AiEngineering, AiSafetyAndSecurity, InfrastructureAndCloud, OpenSourceAndCommunity
- Severity levels: Critical, High, Medium, Low (security items only)
- CI: GitHub Actions with OIDC auth, deploy dev on push to main, prod via manual workflow_dispatch
