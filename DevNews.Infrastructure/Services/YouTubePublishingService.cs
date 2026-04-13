using System.Net.Http.Json;
using System.Text.Json;
using DevNews.Application.Common.Models;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DevNews.Infrastructure.Services;

public class YouTubePublishingService : IPlatformVideoPublisher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<YouTubePublishingService> _logger;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _refreshToken;

    public YouTubePublishingService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<YouTubePublishingService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _clientId = configuration["YouTubeClientId"]
            ?? throw new InvalidOperationException("YouTubeClientId is not configured");
        _clientSecret = configuration["YouTubeClientSecret"]
            ?? throw new InvalidOperationException("YouTubeClientSecret is not configured");
        _refreshToken = configuration["YouTubeRefreshToken"]
            ?? throw new InvalidOperationException("YouTubeRefreshToken is not configured");
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
            // Get fresh access token
            var accessToken = await GetAccessTokenAsync(ct);
            if (!accessToken.IsSuccess)
                return ResultResponse<PlatformPublishResult>.Failure(accessToken.ErrorMessage);

            // Download video from blob storage
            var videoBytes = await _httpClient.GetByteArrayAsync(videoUrl, ct);

            // Upload to YouTube using resumable upload
            var videoId = await UploadVideoAsync(
                videoBytes, title, description, tags, accessToken.Data!, ct);

            if (!videoId.IsSuccess)
                return ResultResponse<PlatformPublishResult>.Failure(videoId.ErrorMessage);

            var publishedUrl = $"https://youtube.com/shorts/{videoId.Data}";

            _logger.LogInformation("Published YouTube Short: {Url}", publishedUrl);
            return ResultResponse<PlatformPublishResult>.Success(
                new PlatformPublishResult(videoId.Data!, publishedUrl));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "YouTube publishing failed for: {Title}", title);
            return ResultResponse<PlatformPublishResult>.Failure($"YouTube publishing failed: {ex.Message}");
        }
    }

    private async Task<ResultResponse<string>> GetAccessTokenAsync(CancellationToken ct)
    {
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["refresh_token"] = _refreshToken,
            ["grant_type"] = "refresh_token"
        });

        var response = await _httpClient.PostAsync(
            "https://oauth2.googleapis.com/token", tokenRequest, ct);

        if (!response.IsSuccessStatusCode)
            return ResultResponse<string>.Failure("Failed to refresh YouTube access token");

        var tokenResponse = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var accessToken = tokenResponse.GetProperty("access_token").GetString();

        return ResultResponse<string>.Success(accessToken!);
    }

    private async Task<ResultResponse<string>> UploadVideoAsync(
        byte[] videoBytes,
        string title,
        string description,
        string[] tags,
        string accessToken,
        CancellationToken ct)
    {
        // Step 1: Initialize resumable upload
        var metadata = new
        {
            snippet = new
            {
                title,
                description,
                tags,
                categoryId = "28" // Science & Technology
            },
            status = new
            {
                privacyStatus = "public",
                selfDeclaredMadeForKids = false
            }
        };

        var initRequest = new HttpRequestMessage(HttpMethod.Post,
            "https://www.googleapis.com/upload/youtube/v3/videos?uploadType=resumable&part=snippet,status");
        initRequest.Headers.Add("Authorization", $"Bearer {accessToken}");
        initRequest.Content = JsonContent.Create(metadata);
        initRequest.Headers.Add("X-Upload-Content-Type", "video/mp4");
        initRequest.Headers.Add("X-Upload-Content-Length", videoBytes.Length.ToString());

        var initResponse = await _httpClient.SendAsync(initRequest, ct);
        if (!initResponse.IsSuccessStatusCode)
        {
            var error = await initResponse.Content.ReadAsStringAsync(ct);
            return ResultResponse<string>.Failure($"YouTube upload init failed: {error}");
        }

        var uploadUrl = initResponse.Headers.Location?.ToString();
        if (string.IsNullOrEmpty(uploadUrl))
            return ResultResponse<string>.Failure("YouTube did not return upload URL");

        // Step 2: Upload video bytes
        var uploadRequest = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
        uploadRequest.Content = new ByteArrayContent(videoBytes);
        uploadRequest.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("video/mp4");

        var uploadResponse = await _httpClient.SendAsync(uploadRequest, ct);
        if (!uploadResponse.IsSuccessStatusCode)
        {
            var error = await uploadResponse.Content.ReadAsStringAsync(ct);
            return ResultResponse<string>.Failure($"YouTube upload failed: {error}");
        }

        var result = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        var videoId = result.GetProperty("id").GetString();

        return ResultResponse<string>.Success(videoId!);
    }
}
