namespace DevNews.Functions.VideoGeneration;

/// <summary>
/// Result of the entire video generation orchestration.
/// </summary>
public record VideoGenerationResult(
    bool Success,
    string Stage,
    int PublishedCount = 0,
    string? Error = null);

/// <summary>
/// Result of script generation activity.
/// </summary>
public record GenerateScriptResult(
    bool Success,
    Guid? VideoContentId,
    string? Script,
    string? Error);

/// <summary>
/// Result of video generation activity.
/// </summary>
public record GenerateVideoResult(
    bool Success,
    Guid VideoContentId,
    byte[]? VideoData,
    string? Error);

/// <summary>
/// Result of video upload to blob storage.
/// </summary>
public record UploadResult(
    bool Success,
    string? VideoUrl,
    string? Error);

/// <summary>
/// Result of AI video validation.
/// </summary>
public record ValidationResult(
    bool IsValid,
    string Reason);

/// <summary>
/// Request to publish video to a specific platform.
/// </summary>
public record PublishRequest(
    Guid VideoContentId,
    string Platform,
    string VideoUrl,
    string Caption);

/// <summary>
/// Result of publishing to a single platform.
/// </summary>
public record PlatformPublishResult(
    bool Success,
    string Platform,
    string? ExternalId,
    string? Error);
