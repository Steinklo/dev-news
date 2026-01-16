using System.Text.RegularExpressions;
using System.Web;
using DevNews.Domain.Common;

namespace DevNews.Domain.NewsItem.ValueObjects;

/// <summary>
/// Value object representing a validated and canonicalized URL.
/// Canonicalization ensures consistent URL comparison for deduplication.
/// </summary>
public partial class NewsUrl : ValueObject
{
    /// <summary>
    /// The canonicalized URL value (lowercase, no tracking params, no www)
    /// </summary>
    public string Value { get; private set; }

    private static readonly HashSet<string> TrackingParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        // Google Analytics / Marketing
        "utm_source", "utm_medium", "utm_campaign", "utm_term", "utm_content", "utm_id",
        "gclid", "gclsrc", "dclid", "gbraid", "wbraid",
        // Facebook
        "fbclid", "fb_action_ids", "fb_action_types", "fb_source", "fb_ref",
        // Twitter/X
        "twclid",
        // Microsoft/Bing
        "msclkid",
        // General tracking
        "ref", "referer", "referrer", "source", "src",
        "mc_cid", "mc_eid",  // Mailchimp
        "oly_enc_id", "oly_anon_id",  // Omeda
        "_hsenc", "_hsmi", "hsCtaTracking",  // HubSpot
        "vero_id", "vero_conv",  // Vero
        "nr_email_referer",  // NewRelic
        "ncid", "cmpid", "mbid",  // Various news sites
        "_ga", "_gl",  // Google Analytics cookies
        "mkt_tok",  // Marketo
        "trk", "trkCampaign", "trkInfo",  // LinkedIn
        "si",  // Spotify
        "igshid",  // Instagram
        "s_kwcid",  // Adobe Analytics
    };

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

        var canonicalUrl = Canonicalize(uri);

        return ResultResponse<NewsUrl>.Success(new NewsUrl(canonicalUrl));
    }

    private static string Canonicalize(Uri uri)
    {
        // 1. Normalize scheme to https
        var scheme = Uri.UriSchemeHttps;

        // 2. Normalize host: lowercase, remove www prefix
        var host = uri.Host.ToLowerInvariant();
        if (host.StartsWith("www."))
            host = host[4..];

        // 3. Normalize port: omit default ports
        var port = uri.IsDefaultPort ? "" : $":{uri.Port}";

        // 4. Normalize path: decode, lowercase, remove trailing slash, collapse slashes
        var path = Uri.UnescapeDataString(uri.AbsolutePath)
            .ToLowerInvariant()
            .TrimEnd('/');
        path = CollapseSlashesRegex().Replace(path, "/");

        // Ensure path starts with /
        if (string.IsNullOrEmpty(path))
            path = "";

        // 5. Normalize query: remove tracking params, sort remaining, lowercase keys
        var query = CanonicalizeQuery(uri.Query);

        // 6. Remove fragment (anchors don't affect content identity)
        // uri.Fragment is intentionally ignored

        return $"{scheme}://{host}{port}{path}{query}";
    }

    private static string CanonicalizeQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query == "?")
            return "";

        var queryParams = HttpUtility.ParseQueryString(query);
        var filteredParams = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string? key in queryParams.AllKeys)
        {
            if (key == null)
                continue;

            // Skip tracking parameters
            if (TrackingParameters.Contains(key))
                continue;

            // Skip empty values
            var value = queryParams[key];
            if (string.IsNullOrEmpty(value))
                continue;

            filteredParams[key.ToLowerInvariant()] = value;
        }

        if (filteredParams.Count == 0)
            return "";

        var sortedQuery = string.Join("&",
            filteredParams.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        return $"?{sortedQuery}";
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(NewsUrl url) => url.Value;

    internal static NewsUrl Reconstitute(string url) => new(url);

    [GeneratedRegex(@"/+")]
    private static partial Regex CollapseSlashesRegex();
}
