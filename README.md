# DevNews Backend

Serverless backend API for DevNews — an AI developer news aggregator that crawls, deduplicates, and summarizes AI/ML news using Claude.

## Architecture

Clean Architecture with Domain-Driven Design (DDD), running as Azure Functions (.NET 9 isolated worker).

```
┌──────────────────────┐
│  DevNews.Functions   │  Azure Functions triggers + HTTP endpoints
├──────────────────────┤
│  DevNews.Application │  Commands, queries, validators (Mediator + FluentValidation)
├──────────────────────┤
│  DevNews.Domain      │  Entities, value objects, domain events
├──────────────────────┤
│  DevNews.Infrastructure │  Cosmos DB, Anthropic AI, RSS crawling
└──────────────────────┘
```

### Nightly Crawl Pipeline (Durable Functions)

A timer-triggered orchestrator runs nightly to:

1. **Discover** articles from RSS feeds via `AiCrawlService`
2. **Deduplicate** against existing items via `AIDuplicationService`
3. **Curate** — AI generates TL;DR summaries, categories, and relevance scores via `AiCurationService` (Anthropic Claude)
4. **Persist** to Cosmos DB

### REST API

| Endpoint | Description |
|---|---|
| `GET /api/v1/news/categories` | List all categories |
| `GET /api/v1/news/category/{category}?year_month=YYYY-MM` | News items by category and month |

## Tech Stack

- **.NET 9** (isolated worker, Azure Functions V4)
- **Azure Cosmos DB** (SQL API) — document storage
- **Azure Key Vault** — secrets management
- **Anthropic Claude** — AI-powered article curation and deduplication
- **Mediator** (source-generated) — CQRS command/query handling
- **FluentValidation** — input validation
- **Application Insights** — telemetry and logging
- **Durable Functions** — orchestrated nightly crawl pipeline
- **xUnit** — unit testing

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local)
- Azure Cosmos DB (or emulator)
- Anthropic API key

## Getting Started

```bash
# Restore dependencies
dotnet restore DevNews.sln

# Build
dotnet build DevNews.sln --configuration Release

# Run tests
dotnet test DevNews.UnitTests/DevNews.UnitTests.csproj

# Run locally
cd DevNews.Functions
func start
```

Configure `local.settings.json` in `DevNews.Functions/` with Cosmos DB and Anthropic API credentials.

## Project Structure

```
DevNews.sln
├── DevNews.Domain/              # Domain layer (no external dependencies)
│   ├── Common/                  # AggregateRoot, Entity, ValueObject, DomainEvent
│   └── NewsItem/                # NewsItem aggregate, enums, value objects, events
├── DevNews.Application/         # Application layer
│   ├── Common/                  # Behaviours (logging, validation, perf), repositories, services
│   └── NewsItem/                # Commands (discover, dedupe, curate, persist), queries, DTOs
├── DevNews.Infrastructure/      # Infrastructure layer
│   ├── Persistence/             # Cosmos DB document models
│   ├── Repositories/            # NewsItemCosmosRepository
│   └── Services/                # AI services (Anthropic), crawl service, curation, deduplication
├── DevNews.Functions/           # Azure Functions entry point
│   ├── NewsApi/                 # HTTP endpoints
│   └── NightlyCrawl/           # Durable Functions orchestrator, activities, triggers
├── DevNews.UnitTests/           # xUnit tests
└── .github/workflows/
    ├── deploy.yml               # Build → deploy dev → deploy prod (OIDC auth)
    └── pr-build.yml             # PR validation (build + test)
```

## CI/CD

- **PR builds**: Restore, build, test, publish artifact
- **Deploy** (on push to `main`): Build → deploy to dev → deploy to prod (Azure Functions, OIDC federated credentials)

## Categories

1. AI Models & APIs
2. AI Developer Tools
3. Agents & Frameworks
4. AI Engineering
5. AI Safety & Security
6. Infrastructure & Cloud
7. Open Source & Community
