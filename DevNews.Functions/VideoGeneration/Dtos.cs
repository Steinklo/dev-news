using System.Text.Json.Serialization;
using DevNews.Domain.Common.Enums;

namespace DevNews.Functions.VideoGeneration;

public record VideoGenerationResult(
    [property: JsonPropertyName("eligible")] int Eligible,
    [property: JsonPropertyName("scriptsGenerated")] int ScriptsGenerated,
    [property: JsonPropertyName("videosGenerated")] int VideosGenerated,
    [property: JsonPropertyName("published")] int Published,
    [property: JsonPropertyName("failed")] int Failed,
    [property: JsonPropertyName("duration")] TimeSpan Duration);

public record ScriptGenerationInput(
    Guid NewsItemId,
    string Title,
    string Summary,
    string Category,
    IReadOnlyList<string> Tags);

public record ScriptValidationInput(
    string Script,
    string OriginalSummary);

public record VideoGenerationInput(
    Guid NewsItemId,
    string Script,
    string Title);

public record GeneratedVideoOutput(
    string VideoUrl,
    int DurationSeconds);

public record PublishInput(
    string VideoUrl,
    string Title,
    string Description,
    string[] Tags,
    Platform Platform);

public record PublishOutput(
    Platform Platform,
    string ExternalId,
    string PublishedUrl);

public record PersistVideoInput(
    Guid NewsItemId,
    string Script,
    int DurationSeconds,
    string VideoUrl,
    IReadOnlyList<PublishOutput>? Publications);
