# Video Generation & Publishing Feature - Implementation Plan

## Overview
Add capability to generate generic short-form videos (TikTok/Instagram/YouTube Shorts/X) from NewsItems, validate them using AI, and automatically publish to social platforms.

**User Requirements:**
- One generic video per NewsItem (works on all platforms)
- AI generates AND validates the video
- Automatic publishing after validation
- Rejected videos saved with reason for review
- Azure Blob Storage for video files

## Architecture Decisions

### 1. Domain Model: VideoContent Aggregate
Create new aggregate root `VideoContent` (separate from NewsItem):
- **Lifecycle**: Draft → AIValidated → Published → Failed
- **Rationale**: Different bounded context (content production vs news curation)
- One-to-one relationship with NewsItem (via NewsItemId reference)
- Stores video metadata, script, validation status, publish results

### 2. Video Generation Flow
```
NewsItem Created
  → VideoGenerationRequested Event (Domain Event)
  → Orchestrator starts
  → Generate Script (AI Activity)
  → Generate Video (AI Activity with script)
  → Validate Video (AI Activity)
  → Upload to Blob Storage
  → Publish to Platforms (Parallel)
  → Mark VideoContent as Published
```

### 3. Technology Stack
- **Video Generation**: External AI video service (e.g., Synthesia, D-ID, or OpenAI + FFmpeg)
- **Storage**: Azure Blob Storage (hot tier)
- **Publishing**: Platform SDKs (Twitter API, Meta Graph API, YouTube Data API)
- **Orchestration**: Azure Durable Functions (matching existing NightlyCrawl pattern)

---

## Implementation Phases

## Phase 1: Domain Layer - VideoContent Aggregate
**Estimated time**: 3-4 hours

### Files to Create (9 files):
- `DevNews.Domain/VideoContent/VideoContent.cs` - Aggregate root with DDD patterns
- `DevNews.Domain/VideoContent/Enums/VideoStatusEnum.cs`
- `DevNews.Domain/VideoContent/Enums/PublishPlatformEnum.cs`
- `DevNews.Domain/VideoContent/ValueObjects/VideoScript.cs`
- `DevNews.Domain/VideoContent/ValueObjects/VideoDuration.cs`
- `DevNews.Domain/VideoContent/ValueObjects/PlatformPublishResult.cs`
- `DevNews.Domain/VideoContent/Events/VideoGenerationRequestedEvent.cs`
- `DevNews.Domain/VideoContent/Events/VideoValidatedEvent.cs`
- `DevNews.Domain/VideoContent/Events/VideoPublishedEvent.cs`

---

## Phase 2: Application Layer - Commands & Queries
**Estimated time**: 4-5 hours

### Files to Create (11 files):
- Commands:
  - `DevNews.Application/VideoContent/Commands/GenerateVideoCommand.cs`
  - `DevNews.Application/VideoContent/Commands/ValidateVideoCommand.cs`
  - `DevNews.Application/VideoContent/Commands/PublishVideoCommand.cs`
  - `DevNews.Application/VideoContent/Commands/MarkVideoRejectedCommand.cs`
  - `DevNews.Application/VideoContent/Commands/UpdateVideoUrlCommand.cs`
  - `DevNews.Application/VideoContent/Commands/AddPublishResultCommand.cs`
  - `DevNews.Application/VideoContent/Commands/MarkVideoPublishedCommand.cs`

- Queries:
  - `DevNews.Application/VideoContent/Queries/GetVideoByNewsItemIdQuery.cs`
  - `DevNews.Application/VideoContent/Queries/GetVideosByStatusQuery.cs`

- DTOs:
  - `DevNews.Application/VideoContent/Dtos/VideoContentDto.cs`

- Interfaces (Application/Common/Services/):
  - `IVideoScriptGenerator.cs`
  - `IVideoGenerator.cs`
  - `IVideoValidationService.cs`
  - `IVideoPublishingService.cs`
  - `IVideoBlobStorage.cs`

- Repositories:
  - `DevNews.Application/Common/Repositories/IVideoContentRepository.cs`

---

## Phase 3: Infrastructure Layer - Service Implementations
**Estimated time**: 6-8 hours (includes external API integration research)

### Files to Create (7 files):
- `DevNews.Infrastructure/Repositories/VideoContentCosmosRepository.cs`
- `DevNews.Infrastructure/Persistence/VideoContentDocument.cs`
- `DevNews.Infrastructure/Services/AiVideoScriptGenerator.cs`
- `DevNews.Infrastructure/Services/VideoGeneratorService.cs` (placeholder)
- `DevNews.Infrastructure/Services/AiVideoValidationService.cs`
- `DevNews.Infrastructure/Services/VideoBlobStorageService.cs`
- `DevNews.Infrastructure/Services/VideoPublishingService.cs` (placeholder)

### Files to Modify:
- `DevNews.Infrastructure/ConfigureServices.cs` - Register video services

---

## Phase 4: Azure Functions - Video Orchestration
**Estimated time**: 3-4 hours

### Files to Create (4 files):
- `DevNews.Functions/VideoGeneration/VideoOrchestrator.cs`
- `DevNews.Functions/VideoGeneration/VideoActivities.cs`
- `DevNews.Functions/VideoGeneration/VideoTriggers.cs`
- `DevNews.Functions/VideoGeneration/Dtos.cs`

---

## Phase 5: Integration with NewsItem Creation
**Estimated time**: 1 hour

### Files to Create:
- `DevNews.Application/NewsItem/EventHandlers/NewsCreatedEventHandler.cs`

### Files to Modify:
- `DevNews.Application/ConfigureServices.cs` - Register event handler

---

## Phase 6: API Endpoints
**Estimated time**: 1 hour

### Files to Create:
- `DevNews.Functions/VideoApi/VideoEndpoints.cs`

---

## Testing Strategy

### Unit Tests (Phase 7): 4-5 hours
1. VideoContent aggregate behavior (Create, Validate, Reject, Publish)
2. Value object validation (VideoScript word count, VideoDuration range)
3. Domain events raised correctly
4. Command handlers return proper ResultResponse

### Integration Tests (Phase 8): 3-4 hours
1. Video orchestration end-to-end (mock external APIs)
2. Blob storage upload/retrieval
3. Cosmos DB repository operations
4. API endpoint responses

### Manual Testing:
1. Create a NewsItem → verify video orchestration triggers
2. Check video generation status endpoint
3. Verify blob storage contains video file
4. Check VideoContent status progression (Draft → Validated → Published)
5. Verify rejected videos saved with reasons

---

## Configuration Required

### appsettings.json additions:
```json
{
  "AzureBlobStorage": {
    "ConnectionString": "UseDevelopmentStorage=true"
  },
  "VideoGeneration": {
    "MaxDurationSeconds": 60,
    "DefaultDurationSeconds": 30
  },
  "SocialPlatforms": {
    "Twitter": {
      "ApiKey": "",
      "ApiSecret": "",
      "AccessToken": "",
      "AccessSecret": ""
    },
    "Instagram": {
      "AccessToken": ""
    },
    "TikTok": {
      "ApiKey": "",
      "AccessToken": ""
    },
    "YouTube": {
      "ClientId": "",
      "ClientSecret": ""
    }
  }
}
```

---

## Deployment Checklist

1. ✅ Create Cosmos DB container: `VideoContent` with partition key `/newsItemId`
2. ✅ Create Azure Blob Storage container: `videos` with public read access
3. ⬜ Configure social platform API credentials
4. ⬜ Deploy Functions app with new orchestrations
5. ⬜ Test video generation flow end-to-end
6. ⬜ Monitor Application Insights for errors

---

## Dependencies to Add

```xml
<!-- DevNews.Infrastructure.csproj -->
<PackageReference Include="Azure.Storage.Blobs" Version="12.19.1" />

<!-- Future: Platform-specific SDKs -->
<!-- <PackageReference Include="TweetinviAPI" Version="5.0.4" /> -->
<!-- <PackageReference Include="Google.Apis.YouTube.v3" Version="1.66.0" /> -->
```

---

## Known Limitations & Future Work

1. **Video generation service**: Placeholder - requires integration with actual service (Synthesia, D-ID, etc.)
2. **Platform publishing**: Requires OAuth setup for each platform
3. **Cost optimization**: No caching of generated videos for similar content
4. **Manual regeneration**: No UI yet for retrying failed videos
5. **Analytics**: No tracking of video performance metrics (views, engagement)
6. **Thumbnail generation**: Videos published without custom thumbnails

---

## Total Estimated Time

- Phase 1 (Domain): 3-4 hours
- Phase 2 (Application): 4-5 hours
- Phase 3 (Infrastructure): 6-8 hours
- Phase 4 (Functions): 3-4 hours
- Phase 5 (Integration): 1 hour
- Phase 6 (API): 1 hour
- Phase 7 (Unit Tests): 4-5 hours
- Phase 8 (Integration Tests): 3-4 hours

**Total: 25-34 hours** (3-4 working days)

---

## Architecture Diagram

```
NewsItem Created
     ↓
NewsCreatedEvent (Domain Event)
     ↓
NewsCreatedEventHandler
     ↓
Start Video Orchestration
     ↓
┌─────────────────────────────────────────┐
│ Video Generation Orchestration          │
├─────────────────────────────────────────┤
│ 1. Generate Script (AI)                 │
│    └→ VideoContent aggregate created    │
│                                          │
│ 2. Generate Video (External API)        │
│    └→ Returns byte[]                    │
│                                          │
│ 3. Upload to Blob Storage               │
│    └→ Returns public URL                │
│    └→ Update VideoContent.BlobStorageUrl│
│                                          │
│ 4. Validate Video (AI)                  │
│    ├─ Valid → Continue                  │
│    └─ Invalid → Mark Rejected, Stop     │
│                                          │
│ 5. Publish (Parallel Fan-out)           │
│    ├─ Twitter API                       │
│    ├─ Instagram API                     │
│    ├─ TikTok API                        │
│    └─ YouTube API                       │
│                                          │
│ 6. Mark VideoContent as Published       │
└─────────────────────────────────────────┘
     ↓
VideoPublishedEvent
```

---

This plan follows Clean Architecture principles, matches existing patterns (Durable Functions orchestration, CQRS, domain events), and implements all user requirements (generic video, AI validation, automatic publishing, rejection tracking, Azure Blob Storage).

**Plan Status**: Approved
**Plan Date**: 2026-01-19
**Estimated Effort**: 25-34 hours
