# DevNews.Domain - Domain Layer Guide

This layer contains core business logic with ZERO external dependencies.

> For project-wide context and global rules, see the root [CLAUDE.md](../CLAUDE.md).

## Folder Structure

```
DevNews.Domain/
  Common/           # Base classes: AggregateRoot, Entity, ValueObject, ResultResponse, DomainEvent
  NewsItem/         # NewsItem aggregate
    Enums/          # CategoryEnum, SeverityEnum
    Events/         # NewsCreatedEvent, NewsUpdatedEvent
    ValueObjects/   # NewsTitle, NewsSummary, NewsUrl, NewsCategory, RelevanceScore
    NewsItem.cs     # Aggregate root
  VideoContent/     # VideoContent aggregate
    Enums/          # VideoStatusEnum, PublishPlatformEnum
    Events/         # VideoGenerationRequestedEvent, VideoValidatedEvent, VideoPublishedEvent
    ValueObjects/   # VideoScript, VideoDuration, PlatformPublishResult
    VideoContent.cs # Aggregate root
```

## Core Patterns

### 1. Aggregate Root Pattern

Aggregate roots inherit from `AggregateRoot<TId>` and are the only entry point for modifying related entities.

```csharp
public class NewsItem : AggregateRoot<Guid>
{
    private readonly List<DomainEvent> _domainEvents = new();
    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    // Properties use Value Objects (not primitives)
    public NewsTitle Title { get; private set; } = null!;
    public NewsSummary Summary { get; private set; } = null!;
    // ...
}
```

### 2. Factory Method Pattern (Create)

Use static `Create()` factory methods returning `ResultResponse<T>`. NEVER throw for validation failures.

```csharp
public static ResultResponse<NewsItem> Create(
    string title,
    string summary,
    // ... other params)
{
    // 1. Create value objects first (they validate themselves)
    var titleResult = NewsTitle.Create(title);
    if (!titleResult.IsSuccess)
        return ResultResponse<NewsItem>.Failure(titleResult.ErrorMessage);

    // 2. Enforce aggregate-level invariants
    if (severity.HasValue && category != CategoryEnum.SecurityAndVulnerabilities)
        return ResultResponse<NewsItem>.Failure("Severity only allowed for security category");

    // 3. Create aggregate using private constructor
    var newsItem = new NewsItem(
        id: Guid.CreateVersion7(),  // Use V7 for time-ordered IDs
        title: titleResult.Data!,
        // ...);

    // 4. Raise domain event
    newsItem._domainEvents.Add(new NewsCreatedEvent(newsItem));

    return ResultResponse<NewsItem>.Success(newsItem);
}
```

### 3. Reconstitute Pattern (for Repository)

Use internal `Reconstitute()` methods to rebuild aggregates from persistence. These bypass validation since data was validated on creation.

```csharp
internal static NewsItem Reconstitute(
    Guid id,
    string title,
    // ... all persisted properties)
{
    var newsItem = new NewsItem(
        id: id,
        title: NewsTitle.Reconstitute(title),  // Use value object's Reconstitute
        // ...);

    newsItem.CreatedAt = createdAt;  // Set readonly properties directly
    return newsItem;
}
```

### 4. Value Object Pattern

Value objects inherit from `ValueObject`, are immutable, and validate their own constraints.

```csharp
public class NewsSummary : ValueObject
{
    public string Value { get; private set; }
    public const int MaxLength = 1000;
    public const int MinLength = 400;

    private NewsSummary(string value) { Value = value; }

    public static ResultResponse<NewsSummary> Create(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            return ResultResponse<NewsSummary>.Failure("Summary cannot be empty");

        var trimmed = summary.Trim();
        if (trimmed.Length < MinLength)
            return ResultResponse<NewsSummary>.Failure($"Summary must be at least {MinLength} characters");

        return ResultResponse<NewsSummary>.Success(new NewsSummary(trimmed));
    }

    internal static NewsSummary Reconstitute(string value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
```

### 5. Domain Events

Raise events for significant state changes. Events are dispatched after persistence.

```csharp
public class NewsCreatedEvent(NewsItem newsItem) : DomainEvent(newsItem.Id)
{
    public NewsItem NewsItem { get; } = newsItem;
}
```

## Adding a New Aggregate

1. Create folder: `DevNews.Domain/[AggregateName]/`
2. Create subfolders: `Enums/`, `Events/`, `ValueObjects/`
3. Create value objects first (they validate domain rules)
4. Create aggregate root with:
   - Private constructor
   - Public static `Create()` factory returning `ResultResponse<T>`
   - Internal static `Reconstitute()` for repository
   - Domain events list
5. NO external dependencies - no using statements from other projects

## Checklist for New Domain Code

- [ ] Uses value objects instead of primitive obsession
- [ ] Factory method returns `ResultResponse<T>`, never throws
- [ ] Has `Reconstitute` method for persistence
- [ ] Raises domain events for significant state changes
- [ ] Immutable (private setters, readonly collections)
- [ ] Zero external dependencies (no Infrastructure/Application references)
- [ ] Uses `Guid.CreateVersion7()` for time-ordered IDs
