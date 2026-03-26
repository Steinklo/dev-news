using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DevNews.Application.Common.Models;
using DevNews.Domain.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DevNews.Infrastructure.Services;

public class TwitterPublishingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TwitterPublishingService> _logger;
    private readonly string _bearerToken;

    public TwitterPublishingService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<TwitterPublishingService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _bearerToken = configuration["TwitterBearerToken"]
            ?? throw new InvalidOperationException("TwitterBearerToken is not configured");

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _bearerToken);
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
            // Step 1: Download video from blob storage
            var videoBytes = await _httpClient.GetByteArrayAsync(videoUrl, ct);

            // Step 2: Upload media via Twitter API v2
            var mediaId = await UploadMediaAsync(videoBytes, ct);
            if (!mediaId.IsSuccess)
                return ResultResponse<PlatformPublishResult>.Failure(mediaId.ErrorMessage);

            // Step 3: Create tweet with media
            var hashtagText = string.Join(" ", tags.Take(3).Select(t => $"#{t}"));
            var tweetText = $"{title}\n\n{hashtagText}".Trim();

            // Truncate to Twitter's 280 char limit
            if (tweetText.Length > 280)
                tweetText = tweetText[..277] + "...";

            var tweetBody = new
            {
                text = tweetText,
                media = new { media_ids = new[] { mediaId.Data } }
            };

            var tweetResponse = await _httpClient.PostAsJsonAsync(
                "https://api.twitter.com/2/tweets", tweetBody, ct);

            if (!tweetResponse.IsSuccessStatusCode)
            {
                var error = await tweetResponse.Content.ReadAsStringAsync(ct);
                return ResultResponse<PlatformPublishResult>.Failure($"Tweet creation failed: {error}");
            }

            var result = await tweetResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
            var tweetId = result.GetProperty("data").GetProperty("id").GetString()!;
            var publishedUrl = $"https://x.com/i/status/{tweetId}";

            _logger.LogInformation("Published to X/Twitter: {Url}", publishedUrl);
            return ResultResponse<PlatformPublishResult>.Success(
                new PlatformPublishResult(tweetId, publishedUrl));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Twitter publishing failed for: {Title}", title);
            return ResultResponse<PlatformPublishResult>.Failure($"Twitter publishing failed: {ex.Message}");
        }
    }

    private async Task<ResultResponse<string>> UploadMediaAsync(byte[] videoBytes, CancellationToken ct)
    {
        // INIT
        var initContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["command"] = "INIT",
            ["total_bytes"] = videoBytes.Length.ToString(),
            ["media_type"] = "video/mp4",
            ["media_category"] = "tweet_video"
        });

        var initResponse = await _httpClient.PostAsync(
            "https://upload.twitter.com/1.1/media/upload.json", initContent, ct);

        if (!initResponse.IsSuccessStatusCode)
            return ResultResponse<string>.Failure("Twitter media INIT failed");

        var initResult = await initResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        var mediaId = initResult.GetProperty("media_id_string").GetString()!;

        // APPEND (single chunk for short videos)
        var appendContent = new MultipartFormDataContent
        {
            { new StringContent("APPEND"), "command" },
            { new StringContent(mediaId), "media_id" },
            { new StringContent("0"), "segment_index" },
            { new ByteArrayContent(videoBytes), "media_data", "video.mp4" }
        };

        var appendResponse = await _httpClient.PostAsync(
            "https://upload.twitter.com/1.1/media/upload.json", appendContent, ct);

        if (!appendResponse.IsSuccessStatusCode)
            return ResultResponse<string>.Failure("Twitter media APPEND failed");

        // FINALIZE
        var finalizeContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["command"] = "FINALIZE",
            ["media_id"] = mediaId
        });

        var finalizeResponse = await _httpClient.PostAsync(
            "https://upload.twitter.com/1.1/media/upload.json", finalizeContent, ct);

        if (!finalizeResponse.IsSuccessStatusCode)
            return ResultResponse<string>.Failure("Twitter media FINALIZE failed");

        // Poll for processing completion
        var finalizeResult = await finalizeResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        if (finalizeResult.TryGetProperty("processing_info", out _))
        {
            await PollMediaProcessingAsync(mediaId, ct);
        }

        return ResultResponse<string>.Success(mediaId);
    }

    private async Task PollMediaProcessingAsync(string mediaId, CancellationToken ct)
    {
        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(2000, ct);

            var statusResponse = await _httpClient.GetAsync(
                $"https://upload.twitter.com/1.1/media/upload.json?command=STATUS&media_id={mediaId}", ct);

            if (!statusResponse.IsSuccessStatusCode) continue;

            var status = await statusResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
            if (!status.TryGetProperty("processing_info", out var processingInfo)) break;

            var state = processingInfo.GetProperty("state").GetString();
            if (state == "succeeded") break;
            if (state == "failed") break;
        }
    }
}
