using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DevNews.Application.Common.Models;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DevNews.Infrastructure.Services;

/// <summary>
/// Publishes a text post to Bluesky via the AT Protocol (create session with an app password,
/// then create an app.bsky.feed.post record). Optional — degrades gracefully when not configured.
/// </summary>
public class BlueskyPublisher : ISocialPostPublisher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BlueskyPublisher> _logger;
    private readonly string _handle;
    private readonly string _appPassword;

    public string PlatformName => "Bluesky";

    public BlueskyPublisher(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<BlueskyPublisher> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Bluesky is optional — missing credentials must NOT throw; degrade in PublishTextAsync.
        _handle = configuration["BlueskyHandle"] ?? "";
        _appPassword = configuration["BlueskyAppPassword"] ?? "";

        _httpClient.BaseAddress = new Uri("https://bsky.social/xrpc/");
    }

    public async Task<ResultResponse<PlatformPublishResult>> PublishTextAsync(
        string text,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_handle) || string.IsNullOrWhiteSpace(_appPassword))
        {
            _logger.LogInformation("Bluesky publishing skipped — Bluesky credentials are not configured");
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

            // 2) Create the post record. A dictionary is used so the "$type" field serializes literally.
            var createBody = new Dictionary<string, object?>
            {
                ["repo"] = did,
                ["collection"] = "app.bsky.feed.post",
                ["record"] = new Dictionary<string, object?>
                {
                    ["$type"] = "app.bsky.feed.post",
                    ["text"] = text,
                    ["createdAt"] = DateTimeOffset.UtcNow.ToString("o"),
                },
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
                return ResultResponse<PlatformPublishResult>.Failure($"Bluesky post failed: {error}");
            }

            var result = await postResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
            var uri = result.GetProperty("uri").GetString() ?? "";

            // at://<did>/app.bsky.feed.post/<rkey> -> https://bsky.app/profile/<handle>/post/<rkey>
            var rkey = uri.Contains('/') ? uri[(uri.LastIndexOf('/') + 1)..] : uri;
            var publishedUrl = $"https://bsky.app/profile/{_handle}/post/{rkey}";

            _logger.LogInformation("Published text post to Bluesky: {Url}", publishedUrl);
            return ResultResponse<PlatformPublishResult>.Success(new PlatformPublishResult(uri, publishedUrl));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bluesky publishing failed");
            return ResultResponse<PlatformPublishResult>.Failure($"Bluesky publishing failed: {ex.Message}");
        }
    }
}
