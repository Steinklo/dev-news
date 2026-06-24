using DevNews.Domain.SocialPost;
using DevNews.Domain.SocialPost.Enums;

namespace DevNews.Infrastructure.Persistence;

public class SocialPostDocument
{
    public string id { get; set; } = null!;
    public string Key { get; set; } = null!;
    public string NewsItemId { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string? SourceUrl { get; set; }
    public int Status { get; set; }
    public string? ExternalId { get; set; }
    public string? PublishedUrl { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public static SocialPostDocument FromDomain(SocialPost socialPost)
    {
        return new SocialPostDocument
        {
            id = socialPost.Id.ToString(),
            Key = $"textpost_{socialPost.CreatedAt:yyyy-MM}",
            NewsItemId = socialPost.NewsItemId.ToString(),
            Content = socialPost.Content.Value,
            SourceUrl = socialPost.SourceUrl,
            Status = (int)socialPost.Status,
            ExternalId = socialPost.ExternalId,
            PublishedUrl = socialPost.PublishedUrl,
            PublishedAt = socialPost.PublishedAt,
            CreatedAt = socialPost.CreatedAt,
            UpdatedAt = socialPost.UpdatedAt
        };
    }

    public SocialPost ToDomain()
    {
        return SocialPost.Reconstitute(
            id: Guid.Parse(id),
            newsItemId: Guid.Parse(NewsItemId),
            content: Content,
            sourceUrl: SourceUrl,
            status: (SocialPostStatus)Status,
            externalId: ExternalId,
            publishedUrl: PublishedUrl,
            publishedAt: PublishedAt,
            createdAt: CreatedAt,
            updatedAt: UpdatedAt);
    }
}
