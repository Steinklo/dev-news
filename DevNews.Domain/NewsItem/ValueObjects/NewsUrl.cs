using DevNews.Domain.Common;

namespace DevNews.Domain.NewsItem.ValueObjects;

/// <summary>
/// Value object representing a validated and normalized URL
/// </summary>
public class NewsUrl : ValueObject
{
    public string Value { get; private set; }

    private NewsUrl(string value)
    {
        Value = value;
    }

    public static ResultResponse<NewsUrl> Create(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return ResultResponse<NewsUrl>.Failure("URL cannot be empty");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return ResultResponse<NewsUrl>.Failure("URL must be a valid absolute URL");

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return ResultResponse<NewsUrl>.Failure("URL must use HTTP or HTTPS protocol");

        // Normalize: remove trailing slash for consistency
        var normalized = uri.ToString().TrimEnd('/');

        return ResultResponse<NewsUrl>.Success(new NewsUrl(normalized));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value.ToLowerInvariant();
    }

    public override string ToString() => Value;

    public static implicit operator string(NewsUrl url) => url.Value;
}
