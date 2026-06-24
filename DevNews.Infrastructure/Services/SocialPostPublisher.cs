using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DevNews.Application.Common.Models;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DevNews.Infrastructure.Services;

public class SocialPostPublisher : ISocialPostPublisher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SocialPostPublisher> _logger;
    private readonly string _accessToken;
    private readonly string _organizationId;

    public string PlatformName => "LinkedIn";

    public SocialPostPublisher(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SocialPostPublisher> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // LinkedIn is optional — missing credentials must NOT throw here (see LinkedInPublishingService).
        _accessToken = configuration["LinkedInAccessToken"] ?? "";
        _organizationId = configuration["VideoGeneration:LinkedInOrganizationId"] ?? "";

        _httpClient.BaseAddress = new Uri("https://api.linkedin.com/v2/");
        if (!string.IsNullOrWhiteSpace(_accessToken))
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    public async Task<ResultResponse<PlatformPublishResult>> PublishTextAsync(
        string text,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_accessToken) || string.IsNullOrWhiteSpace(_organizationId))
        {
            _logger.LogInformation("LinkedIn text publishing skipped — LinkedIn credentials are not configured");
            return ResultResponse<PlatformPublishResult>.Failure("LinkedIn credentials are not configured");
        }

        try
        {
            var postBody = new
            {
                author = $"urn:li:organization:{_organizationId}",
                lifecycleState = "PUBLISHED",
                specificContent = new
                {
                    com_linkedin_ugc_ShareContent = new
                    {
                        shareCommentary = new { text },
                        shareMediaCategory = "NONE"
                    }
                },
                visibility = new { com_linkedin_ugc_MemberNetworkVisibility = "PUBLIC" }
            };

            var response = await _httpClient.PostAsJsonAsync("ugcPosts", postBody, ct);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                return ResultResponse<PlatformPublishResult>.Failure($"LinkedIn text post failed: {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var postId = result.GetProperty("id").GetString() ?? "";

            var publishedUrl = $"https://www.linkedin.com/feed/update/{postId}";
            _logger.LogInformation("Published text post to LinkedIn: {Url}", publishedUrl);

            return ResultResponse<PlatformPublishResult>.Success(
                new PlatformPublishResult(postId, publishedUrl));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LinkedIn text publishing failed");
            return ResultResponse<PlatformPublishResult>.Failure($"LinkedIn text publishing failed: {ex.Message}");
        }
    }
}
