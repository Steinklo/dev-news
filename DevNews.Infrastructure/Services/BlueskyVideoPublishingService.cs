using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DevNews.Application.Common.Models;
using DevNews.Domain.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DevNews.Infrastructure.Services;

/// <summary>
/// Publishes a short video to Bluesky via the AT Protocol. Bluesky video is not a plain blob
/// upload: bytes go to the dedicated video service (video.bsky.app), which processes them
/// asynchronously, and the resulting blob is embedded in an app.bsky.embed.video post.
/// Reuses the same app-password credentials as <see cref="BlueskyPublisher"/> (text posts).
/// Optional — degrades gracefully when not configured.
/// </summary>
public class BlueskyVideoPublishingService
{
    // Bluesky-hosted accounts cap video at 100 MB; refuse oversized clips before uploading.
    private const long MaxVideoBytes = 100_000_000;
    // The video service processes asynchronously; poll a bounded number of times before giving up.
    private const int MaxJobPollAttempts = 150;
    private static readonly TimeSpan JobPollInterval = TimeSpan.FromSeconds(2);

    private readonly HttpClient _httpClient;
    private readonly ILogger<BlueskyVideoPublishingService> _logger;
    private readonly string _handle;
    private readonly string _appPassword;

    public BlueskyVideoPublishingService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<BlueskyVideoPublishingService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Bluesky is optional — missing credentials must NOT throw; degrade in PublishAsync.
        _handle = configuration["BlueskyHandle"] ?? "";
        _appPassword = configuration["BlueskyAppPassword"] ?? "";

        _httpClient.BaseAddress = new Uri("https://bsky.social/xrpc/");
    }

    public async Task<ResultResponse<PlatformPublishResult>> PublishAsync(
        string videoUrl,
        string title,
        string description,
        string[] tags,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_handle) || string.IsNullOrWhiteSpace(_appPassword))
        {
            _logger.LogInformation("Bluesky video publishing skipped — Bluesky credentials are not configured");
            return ResultResponse<PlatformPublishResult>.Failure("Bluesky credentials are not configured");
        }

        try
        {
            // 1) Create a session (log in with the app password).
            var sessionResponse = await _httpClient.PostAsJsonAsync(
                "com.atproto.server.createSession",
                new { identifier = _handle, password = _appPassword },
                ct);

            if (!sessionResponse.IsSuccessStatusCode)
            {
                var error = await sessionResponse.Content.ReadAsStringAsync(ct);
                return ResultResponse<PlatformPublishResult>.Failure($"Bluesky login failed: {error}");
            }

            var session = await sessionResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
            var accessJwt = session.GetProperty("accessJwt").GetString()!;
            var did = session.GetProperty("did").GetString()!;
            var pdsHost = ResolvePdsHost(session);

            // 2) Download the rendered video from blob storage.
            var videoBytes = await _httpClient.GetByteArrayAsync(videoUrl, ct);
            if (videoBytes.LongLength > MaxVideoBytes)
                return ResultResponse<PlatformPublishResult>.Failure(
                    $"Video is {videoBytes.LongLength} bytes, over Bluesky's {MaxVideoBytes} byte limit");

            // 3) Mint a service-auth token for the upload. Counter-intuitively the audience is the
            //    user's PDS (the blob lands there) and the method is uploadBlob, not the video method.
            var serviceToken = await GetServiceAuthAsync(accessJwt, pdsHost, ct);
            if (!serviceToken.IsSuccess)
                return ResultResponse<PlatformPublishResult>.Failure(serviceToken.ErrorMessage);

            // 4) Upload the bytes to the video service and wait for processing to finish.
            var blob = await UploadAndAwaitBlobAsync(videoBytes, did, serviceToken.Data!, ct);
            if (!blob.IsSuccess)
                return ResultResponse<PlatformPublishResult>.Failure(blob.ErrorMessage);

            // 5) Create the post embedding the processed video blob.
            return await CreateVideoPostAsync(did, accessJwt, blob.Data, title, description, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bluesky video publishing failed for: {Title}", title);
            return ResultResponse<PlatformPublishResult>.Failure($"Bluesky video publishing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Mints a short-lived service-auth token. The audience is the account's PDS DID and the
    /// declared method (lxm) is com.atproto.repo.uploadBlob — both are required by the video service.
    /// </summary>
    private async Task<ResultResponse<string>> GetServiceAuthAsync(
        string accessJwt, string pdsHost, CancellationToken ct)
    {
        var aud = Uri.EscapeDataString($"did:web:{pdsHost}");
        var lxm = Uri.EscapeDataString("com.atproto.repo.uploadBlob");
        var exp = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"com.atproto.server.getServiceAuth?aud={aud}&lxm={lxm}&exp={exp}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessJwt);

        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return ResultResponse<string>.Failure($"Bluesky service-auth failed: {error}");
        }

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return ResultResponse<string>.Success(body.GetProperty("token").GetString()!);
    }

    /// <summary>
    /// Pushes raw MP4 bytes to video.bsky.app and polls the async job until the processed blob is
    /// ready. Returns the blob ref (cloned) to embed verbatim in the post.
    /// </summary>
    private async Task<ResultResponse<JsonElement>> UploadAndAwaitBlobAsync(
        byte[] videoBytes, string did, string serviceToken, CancellationToken ct)
    {
        var name = $"devnews-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.mp4";
        using var uploadRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://video.bsky.app/xrpc/app.bsky.video.uploadVideo?did={Uri.EscapeDataString(did)}&name={name}");
        uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceToken);
        uploadRequest.Content = new ByteArrayContent(videoBytes);
        uploadRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");

        var uploadResponse = await _httpClient.SendAsync(uploadRequest, ct);
        var uploadBody = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>(ct);

        // Re-uploading identical bytes yields an error but still returns the existing blob — accept that.
        if (TryGetCompletedBlob(uploadBody, out var existingBlob))
            return ResultResponse<JsonElement>.Success(existingBlob);

        if (!uploadResponse.IsSuccessStatusCode || !TryGetJobId(uploadBody, out var jobId))
            return ResultResponse<JsonElement>.Failure(
                $"Bluesky video upload failed: {uploadBody.GetRawText()}");

        // Poll the job until the blob is ready or it fails.
        for (var attempt = 0; attempt < MaxJobPollAttempts; attempt++)
        {
            await Task.Delay(JobPollInterval, ct);

            using var statusRequest = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://video.bsky.app/xrpc/app.bsky.video.getJobStatus?jobId={Uri.EscapeDataString(jobId)}");
            statusRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceToken);

            var statusResponse = await _httpClient.SendAsync(statusRequest, ct);
            if (!statusResponse.IsSuccessStatusCode)
            {
                // Transient error mid-processing — keep polling rather than aborting the whole upload.
                _logger.LogWarning("Bluesky job status poll returned {Status}, retrying", statusResponse.StatusCode);
                continue;
            }

            var statusBody = await statusResponse.Content.ReadFromJsonAsync<JsonElement>(ct);

            if (TryGetCompletedBlob(statusBody, out var blob))
                return ResultResponse<JsonElement>.Success(blob);

            if (statusBody.TryGetProperty("jobStatus", out var jobStatus)
                && jobStatus.TryGetProperty("state", out var state)
                && state.GetString() == "JOB_STATE_FAILED")
            {
                var message = jobStatus.TryGetProperty("error", out var err) ? err.GetString() : "unknown error";
                return ResultResponse<JsonElement>.Failure($"Bluesky video processing failed: {message}");
            }
        }

        return ResultResponse<JsonElement>.Failure("Bluesky video processing timed out");
    }

    private async Task<ResultResponse<PlatformPublishResult>> CreateVideoPostAsync(
        string did, string accessJwt, JsonElement blob, string title, string description, CancellationToken ct)
    {
        // Bluesky post text is capped at 300 graphemes — keep the headline as the caption and use
        // the longer script as accessibility alt text (≤1000 graphemes).
        var record = new Dictionary<string, object?>
        {
            ["$type"] = "app.bsky.feed.post",
            ["text"] = Truncate(title, 300),
            ["createdAt"] = DateTimeOffset.UtcNow.ToString("o"),
            ["embed"] = new Dictionary<string, object?>
            {
                ["$type"] = "app.bsky.embed.video",
                ["video"] = blob, // embed the processed blob verbatim
                ["aspectRatio"] = new Dictionary<string, object?> { ["width"] = 1080, ["height"] = 1920 },
                ["alt"] = Truncate(string.IsNullOrWhiteSpace(description) ? title : description, 1000),
            },
        };

        var createBody = new Dictionary<string, object?>
        {
            ["repo"] = did,
            ["collection"] = "app.bsky.feed.post",
            ["record"] = record,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "com.atproto.repo.createRecord")
        {
            Content = JsonContent.Create(createBody),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessJwt);

        var postResponse = await _httpClient.SendAsync(request, ct);
        if (!postResponse.IsSuccessStatusCode)
        {
            var error = await postResponse.Content.ReadAsStringAsync(ct);
            return ResultResponse<PlatformPublishResult>.Failure($"Bluesky video post failed: {error}");
        }

        var result = await postResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        var uri = result.GetProperty("uri").GetString() ?? "";

        // at://<did>/app.bsky.feed.post/<rkey> -> https://bsky.app/profile/<handle>/post/<rkey>
        var rkey = uri.Contains('/') ? uri[(uri.LastIndexOf('/') + 1)..] : uri;
        var publishedUrl = $"https://bsky.app/profile/{_handle}/post/{rkey}";

        _logger.LogInformation("Published video to Bluesky: {Url}", publishedUrl);
        return ResultResponse<PlatformPublishResult>.Success(new PlatformPublishResult(uri, publishedUrl));
    }

    /// <summary>
    /// Resolves the account's PDS host from the session's DID document, falling back to bsky.social.
    /// The service-auth audience must target the PDS that ultimately stores the blob.
    /// </summary>
    internal static string ResolvePdsHost(JsonElement session)
    {
        if (session.TryGetProperty("didDoc", out var didDoc)
            && didDoc.TryGetProperty("service", out var services)
            && services.ValueKind == JsonValueKind.Array)
        {
            foreach (var svc in services.EnumerateArray())
            {
                if (svc.TryGetProperty("id", out var id)
                    && id.GetString()?.EndsWith("#atproto_pds", StringComparison.Ordinal) == true
                    && svc.TryGetProperty("serviceEndpoint", out var endpoint)
                    && Uri.TryCreate(endpoint.GetString(), UriKind.Absolute, out var endpointUri))
                {
                    return endpointUri.Host;
                }
            }
        }

        return "bsky.social";
    }

    private static bool TryGetJobId(JsonElement body, out string jobId)
    {
        jobId = "";
        if (body.TryGetProperty("jobStatus", out var jobStatus)
            && jobStatus.TryGetProperty("jobId", out var id)
            && id.GetString() is { Length: > 0 } value)
        {
            jobId = value;
            return true;
        }

        return false;
    }

    private static bool TryGetCompletedBlob(JsonElement body, out JsonElement blob)
    {
        blob = default;
        if (body.TryGetProperty("jobStatus", out var jobStatus)
            && jobStatus.TryGetProperty("blob", out var blobElement)
            && blobElement.ValueKind == JsonValueKind.Object)
        {
            blob = blobElement.Clone();
            return true;
        }

        return false;
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max];
}
