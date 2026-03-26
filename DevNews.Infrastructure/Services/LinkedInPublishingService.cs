using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DevNews.Application.Common.Models;
using DevNews.Domain.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DevNews.Infrastructure.Services;

public class LinkedInPublishingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LinkedInPublishingService> _logger;
    private readonly string _accessToken;
    private readonly string _organizationId;

    public LinkedInPublishingService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<LinkedInPublishingService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _accessToken = configuration["LinkedInAccessToken"]
            ?? throw new InvalidOperationException("LinkedInAccessToken is not configured");
        _organizationId = configuration["VideoGeneration:LinkedInOrganizationId"]
            ?? throw new InvalidOperationException("VideoGeneration:LinkedInOrganizationId is not configured");

        _httpClient.BaseAddress = new Uri("https://api.linkedin.com/v2/");
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    public async Task<ResultResponse<PlatformPublishResult>> PublishAsync(
        string videoUrl,
        string title,
        string description,
        string[] tags,
        CancellationToken ct = default)
    {
        try
        {
            // Step 1: Register video upload
            var registerResult = await RegisterUploadAsync(ct);
            if (!registerResult.IsSuccess)
                return ResultResponse<PlatformPublishResult>.Failure(registerResult.ErrorMessage);

            var (uploadUrl, videoUrn) = registerResult.Data!;

            // Step 2: Download video from blob storage and upload to LinkedIn
            var videoBytes = await _httpClient.GetByteArrayAsync(videoUrl, ct);

            var uploadRequest = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
            uploadRequest.Content = new ByteArrayContent(videoBytes);
            uploadRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");

            var uploadResponse = await _httpClient.SendAsync(uploadRequest, ct);
            if (!uploadResponse.IsSuccessStatusCode)
                return ResultResponse<PlatformPublishResult>.Failure("Failed to upload video to LinkedIn");

            // Step 3: Create post with the video
            var hashtagText = string.Join(" ", tags.Take(3).Select(t => $"#{t}"));
            var postBody = new
            {
                author = $"urn:li:organization:{_organizationId}",
                lifecycleState = "PUBLISHED",
                specificContent = new
                {
                    com_linkedin_ugc_ShareContent = new
                    {
                        shareCommentary = new { text = $"{title}\n\n{description}\n\n{hashtagText}" },
                        shareMediaCategory = "VIDEO",
                        media = new[]
                        {
                            new
                            {
                                status = "READY",
                                media = videoUrn,
                                title = new { text = title }
                            }
                        }
                    }
                },
                visibility = new { com_linkedin_ugc_MemberNetworkVisibility = "PUBLIC" }
            };

            var postResponse = await _httpClient.PostAsJsonAsync("ugcPosts", postBody, ct);
            if (!postResponse.IsSuccessStatusCode)
            {
                var error = await postResponse.Content.ReadAsStringAsync(ct);
                return ResultResponse<PlatformPublishResult>.Failure($"LinkedIn post creation failed: {error}");
            }

            var postResult = await postResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
            var postId = postResult.GetProperty("id").GetString() ?? videoUrn;

            var publishedUrl = $"https://www.linkedin.com/feed/update/{postId}";
            _logger.LogInformation("Published to LinkedIn: {Url}", publishedUrl);

            return ResultResponse<PlatformPublishResult>.Success(
                new PlatformPublishResult(postId, publishedUrl));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LinkedIn publishing failed for: {Title}", title);
            return ResultResponse<PlatformPublishResult>.Failure($"LinkedIn publishing failed: {ex.Message}");
        }
    }

    private async Task<ResultResponse<(string UploadUrl, string VideoUrn)>> RegisterUploadAsync(CancellationToken ct)
    {
        var registerBody = new
        {
            registerUploadRequest = new
            {
                recipes = new[] { "urn:li:digitalmediaRecipe:feedshare-video" },
                owner = $"urn:li:organization:{_organizationId}",
                serviceRelationships = new[]
                {
                    new
                    {
                        relationshipType = "OWNER",
                        identifier = "urn:li:userGeneratedContent"
                    }
                }
            }
        };

        var response = await _httpClient.PostAsJsonAsync("assets?action=registerUpload", registerBody, ct);
        if (!response.IsSuccessStatusCode)
            return ResultResponse<(string, string)>.Failure("Failed to register LinkedIn upload");

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var uploadUrl = result
            .GetProperty("value")
            .GetProperty("uploadMechanism")
            .GetProperty("com.linkedin.digitalmedia.uploading.MediaUploadHttpRequest")
            .GetProperty("uploadUrl")
            .GetString()!;

        var videoUrn = result.GetProperty("value").GetProperty("asset").GetString()!;

        return ResultResponse<(string, string)>.Success((uploadUrl, videoUrn));
    }
}
