using DevNews.Domain.Common;
using DevNews.Domain.SocialPost.Enums;
using DevNews.Domain.SocialPost.ValueObjects;

namespace DevNews.Domain.SocialPost;

public class SocialPost : AggregateRoot<Guid>
{
    public Guid NewsItemId { get; private set; }
    public SocialPostText Content { get; private set; } = null!;
    public string? SourceUrl { get; private set; }
    public SocialPostStatus Status { get; private set; }
    public string? ExternalId { get; private set; }
    public string? PublishedUrl { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    private SocialPost(
        Guid id,
        Guid newsItemId,
        SocialPostText content,
        string? sourceUrl,
        SocialPostStatus status) : base(id)
    {
        NewsItemId = newsItemId;
        Content = content;
        SourceUrl = sourceUrl;
        Status = status;
    }

    public static ResultResponse<SocialPost> Create(Guid newsItemId, string content, string? sourceUrl = null)
    {
        if (newsItemId == Guid.Empty)
            return ResultResponse<SocialPost>.Failure("NewsItemId is required");

        var contentResult = SocialPostText.Create(content);
        if (!contentResult.IsSuccess)
            return ResultResponse<SocialPost>.Failure(contentResult.ErrorMessage);

        var socialPost = new SocialPost(
            id: Guid.CreateVersion7(),
            newsItemId: newsItemId,
            content: contentResult.Data!,
            sourceUrl: sourceUrl,
            status: SocialPostStatus.Generated);

        return ResultResponse<SocialPost>.Success(socialPost);
    }

    public void MarkPublished(string externalId, string publishedUrl)
    {
        ExternalId = externalId;
        PublishedUrl = publishedUrl;
        PublishedAt = DateTimeOffset.UtcNow;
        Status = SocialPostStatus.Published;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkFailed()
    {
        Status = SocialPostStatus.Failed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    internal static SocialPost Reconstitute(
        Guid id,
        Guid newsItemId,
        string content,
        string? sourceUrl,
        SocialPostStatus status,
        string? externalId,
        string? publishedUrl,
        DateTimeOffset? publishedAt,
        DateTimeOffset createdAt,
        DateTimeOffset? updatedAt)
    {
        var socialPost = new SocialPost(
            id: id,
            newsItemId: newsItemId,
            content: SocialPostText.Reconstitute(content),
            sourceUrl: sourceUrl,
            status: status);

        socialPost.ExternalId = externalId;
        socialPost.PublishedUrl = publishedUrl;
        socialPost.PublishedAt = publishedAt;
        socialPost.CreatedAt = createdAt;
        socialPost.UpdatedAt = updatedAt;

        return socialPost;
    }
}
