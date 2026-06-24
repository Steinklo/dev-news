using DevNews.Domain.Common.Enums;
using DevNews.Domain.ShortVideo.Enums;

namespace DevNews.Application.ShortVideo.Dtos;

public record ShortVideoDto(
    Guid Id,
    Guid NewsItemId,
    string Script,
    int DurationSeconds,
    string VideoUrl,
    string? ThumbnailUrl,
    VideoStatus Status,
    IReadOnlyList<PlatformPublicationDto> Publications,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public record PlatformPublicationDto(
    Platform Platform,
    string ExternalId,
    string PublishedUrl,
    DateTimeOffset PublishedAt);
