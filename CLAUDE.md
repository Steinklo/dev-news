# CLAUDE.md

Serverless C# backend for an AI developer-news aggregator + short-video generator. Azure Functions V4 (.NET 10 isolated worker), Cosmos DB, Anthropic Claude. Siblings: `dev-news-frontend`, `dev-news-iac`.

## Tech stack

- .NET 10 (`net10.0`), Azure Functions V4 **isolated worker** with ASP.NET Core integration (`Worker` 2.51.0, `Worker.Sdk` 2.0.7)
- Durable Functions (`Extensions.DurableTask` 1.16.3), Timer trigger (`Extensions.Timer` 4.3.1)
- Mediator 3.0.2 — **source-generated CQRS, NOT MediatR** (`martinothamar/Mediator`)
- FluentValidation 12.1.1; Cosmos DB SDK 3.58.0; Azure Blob 12.27.0
- Anthropic SDK 12.13.0; SmartReader 0.11.0 (article extraction); `System.ServiceModel.Syndication` (RSS)
- OpenTelemetry → Azure Monitor (`Azure.Monitor.OpenTelemetry.AspNetCore` 1.4.0)
- Tests: xUnit 2.9.3, NSubstitute 5.3.0

## Commands

```bash
dotnet restore DevNews.sln
dotnet build DevNews.sln --configuration Release
dotnet test DevNews.UnitTests/DevNews.UnitTests.csproj
cd DevNews.Functions && func start          # serves http://localhost:7071
```

Deploy is CI-only: push to `main` → dev; manual `workflow_dispatch` → prod. Do not deploy by hand. Local `func start` needs Azurite (or a storage account) for Durable Functions; secrets go in `local.settings.json` (gitignored).

## Architecture & key patterns

- Clean Architecture / DDD. Flow: **Functions → Application + Infrastructure → Domain**.
  - `DevNews.Domain` — entities, value objects, enums (depends only on `Mediator.Abstractions`)
  - `DevNews.Application` — Mediator commands/queries/validators, service interfaces
  - `DevNews.Infrastructure` — Cosmos repos, Anthropic, RSS crawl, video, publishing
  - `DevNews.Functions` — HTTP endpoints + Durable orchestrators; DI wired in `Program.cs` via `AddApplicationServices` + `AddInfrastructureServices`
- Value objects enforce invariants: `NewsTitle`, `NewsUrl`, `NewsSummary`, `NewsCategory`, `RelevanceScore`.
- Mediator pipeline behaviours: logging, FluentValidation, performance, exception handling.
- Cosmos: database `dev-news-db`, containers `news-items` and `short-videos` (partition key `/Key`).
- Durable pipelines: `DailyPipelineOrchestrator` (timer `%DailyPipelineSchedule%` ~06:00 UTC + manual) → `NightlyCrawlOrchestrator` (discover → curate → dedupe → persist, relevance ≥ 50) → conditionally `VideoGenerationOrchestrator` (select score 85+, ≤5 → script → validate → Creatomate render → **publish fan-out to YouTube + LinkedIn** → persist). There is no separate "social posts" orchestrator — publishing is the fan-out step inside video generation.
- Endpoints: 3 **Anonymous** GET news endpoints (`GetCategories`, `GetNewsById`, `GetNewsByCategory`); 6 **Function-key** Durable triggers (pipeline / crawl / video-generation start + status).
- Rate limiting: `RateLimitingMiddleware` (registered via `UseMiddleware` in `Program.cs`) — per-IP fixed window 60/min, applies only to the 3 anonymous GETs, returns 429 + `Retry-After`.

## Conventions

- JSON: camelCase, null values omitted (configured in `Program.cs`).
- Categories: fixed 1-based `CategoryEnum`, 7 values in priority order — `AiModelsAndApis`, `AiDeveloperTools`, `AgentsAndFrameworks`, `AiEngineering`, `AiSafetyAndSecurity`, `InfrastructureAndCloud`, `OpenSourceAndCommunity`.
- `SeverityEnum`: Critical/High/Medium/Low (security items only). `Platform`: YouTube, LinkedIn.
- Mediator is source-generated; `MSG0005` is suppressed (domain events raised, not yet dispatched).

## Gotchas

- Telemetry is **OpenTelemetry → Azure Monitor**, not the classic Application Insights SDK (incompatible with .NET 10 isolated worker). Do not re-add `Microsoft.ApplicationInsights*` SDK packages.
- The rate limiter is in-memory, so the limit is enforced **per worker instance, not globally**.
- `func start` requires a storage backend (Azurite / `UseDevelopmentStorage=true` or a real account) for Durable Functions.
- Never commit secrets: `.gitignore` excludes `local.settings.json`, `appsettings.json`, `appsettings.Development.json`. Deployed config lives in Azure App Settings / Key Vault.
- Don't edit `obj/` / `bin/` or Mediator source-generated output. CI ignores `**/*.md` and `.gitignore` for build/deploy triggers.

## Further context

- `README.md` — pipeline overview, API table, configuration.
- Siblings: [`dev-news-frontend`](https://github.com/Steinklo/dev-news-frontend), [`dev-news-iac`](https://github.com/Steinklo/dev-news-iac).
