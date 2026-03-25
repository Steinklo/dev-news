# DevNews Backend

Serverless C# backend for an AI developer news aggregator. Azure Functions V4 (.NET 9 isolated worker) with Cosmos DB and Anthropic Claude AI.

## Commands

```bash
dotnet restore DevNews.sln                                    # Restore
dotnet build DevNews.sln --configuration Release              # Build
dotnet test DevNews.UnitTests/DevNews.UnitTests.csproj        # Test
cd DevNews.Functions && func start                            # Run locally
```

## Project Structure

```
DevNews.Domain/           # Entities, value objects, enums, domain events (zero dependencies)
DevNews.Application/      # Commands, queries, validators, service interfaces (Mediator CQRS)
DevNews.Infrastructure/   # Cosmos DB repos, Anthropic AI services, RSS crawling
DevNews.Functions/        # Azure Functions entry point (HTTP endpoints + Durable Functions)
DevNews.UnitTests/        # xUnit tests
```

**Dependency flow**: Functions → Application + Infrastructure → Domain

## Key Patterns

- **Clean Architecture / DDD**: Domain has no external dependencies; Application defines interfaces; Infrastructure implements them
- **CQRS via Mediator**: Source-generated Mediator for commands and queries (not MediatR)
- **Pipeline behaviours**: Logging, validation (FluentValidation), performance tracking, exception handling
- **Value objects**: NewsTitle, NewsUrl, NewsSummary, NewsCategory, RelevanceScore — enforce invariants at domain level
- **Durable Functions orchestrator**: Nightly crawl pipeline (discover → deduplicate → curate → persist)

## API Endpoints

- `GET /api/v1/news/categories` → `CategoriesResponse`
- `GET /api/v1/news/category/{category}?year_month=YYYY-MM` → `NewsByCategoryResponse`

## Infrastructure Services

- `AnthropicAiService` — Claude API wrapper
- `AiCrawlService` — RSS feed discovery + article extraction (SmartReader)
- `AiCurationService` — AI-generated summaries, categories, relevance scores
- `AIDuplicationService` — AI-powered deduplication
- `NewsItemCosmosRepository` — Cosmos DB persistence

## Conventions

- JSON serialization: camelCase, null values omitted
- Nullable reference types enabled throughout
- Categories are a fixed enum (7 values, AI-focused, ordered by priority)
- Severity levels: Critical, High, Medium, Low (security items only)
- CI: GitHub Actions with OIDC auth, deploy dev → prod on push to main
