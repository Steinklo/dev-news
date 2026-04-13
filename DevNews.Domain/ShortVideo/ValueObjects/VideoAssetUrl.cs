using DevNews.Domain.Common;

namespace DevNews.Domain.ShortVideo.ValueObjects;

/// <summary>
/// Value object representing a URL to a video or thumbnail asset in blob storage.
/// </summary>
public class VideoAssetUrl : ValueObject
{
    public string Value { get; private set; }

    private VideoAssetUrl(string value)
    {
        Value = value;
    }

    public static ResultResponse<VideoAssetUrl> Create(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return ResultResponse<VideoAssetUrl>.Failure("Asset URL cannot be empty");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return ResultResponse<VideoAssetUrl>.Failure("Asset URL must be a valid absolute URL");

        if (uri.Scheme != "https")
            return ResultResponse<VideoAssetUrl>.Failure("Asset URL must use HTTPS");

        return ResultResponse<VideoAssetUrl>.Success(new VideoAssetUrl(uri.ToString()));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(VideoAssetUrl url) => url.Value;

    internal static VideoAssetUrl Reconstitute(string value) => new(value);
}
