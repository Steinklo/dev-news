using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevNews.Application.Common.Models;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DevNews.Infrastructure.Services;

public class CreatomateVideoGenerationService : IVideoGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CreatomateVideoGenerationService> _logger;
    private readonly string _apiKey;
    private readonly string _voiceName;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public CreatomateVideoGenerationService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<CreatomateVideoGenerationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["CreatomateApiKey"]
            ?? throw new InvalidOperationException("CreatomateApiKey is not configured");
        _voiceName = configuration["VideoGeneration:TtsVoiceName"] ?? "en-US-AndrewMultilingualNeural";

        _httpClient.BaseAddress = new Uri("https://api.creatomate.com/v1/");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    public async Task<ResultResponse<GeneratedVideo>> GenerateVideoAsync(
        string script,
        string title,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogDebug("Building programmatic video source for: {Title}", title);

            var source = BuildVideoSource(script, title);

            var requestBody = new { source };

            var response = await _httpClient.PostAsJsonAsync("renders", requestBody, JsonOptions, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Creatomate render failed ({Status}): {Error}",
                    response.StatusCode, errorBody);
                return ResultResponse<GeneratedVideo>.Failure(
                    $"Creatomate render failed: {response.StatusCode}");
            }

            var renderResponse = await response.Content.ReadFromJsonAsync<JsonElement[]>(ct);
            if (renderResponse == null || renderResponse.Length == 0)
                return ResultResponse<GeneratedVideo>.Failure("Empty render response from Creatomate");

            var render = renderResponse[0];
            var renderId = render.GetProperty("id").GetString()!;

            var videoResult = await PollRenderCompletion(renderId, ct);
            return videoResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Video generation failed for: {Title}", title);
            return ResultResponse<GeneratedVideo>.Failure($"Video generation failed: {ex.Message}");
        }
    }

    internal object BuildVideoSource(string script, string title)
    {
        var imagePrompt = $"A cinematic wide-angle photo related to: {title}. " +
                          "Dark moody tech aesthetic, suitable as video background. No text, no logos.";

        return new
        {
            output_format = "mp4",
            width = 1080,
            height = 1920,
            elements = new object[]
            {
                // Background image (AI-generated via DALL-E)
                new
                {
                    type = "image",
                    provider = "openai model_id=dall-e-3 style=vivid",
                    source = imagePrompt,
                    width = "100%",
                    height = "100%",
                    color_overlay = "rgba(0,0,0,0.55)",
                },
                // Title text
                new
                {
                    type = "text",
                    track = 1,
                    text = title,
                    y = "15%",
                    width = "90%",
                    x_alignment = "50%",
                    y_alignment = "50%",
                    fill_color = "#ffffff",
                    font_family = "Inter",
                    font_weight = "800",
                    font_size = "8 vmin",
                    animations = new object[]
                    {
                        new { type = "slide", direction = "0°", duration = "1 s" },
                    },
                },
                // Script text with appear animation
                new
                {
                    type = "text",
                    track = 2,
                    text = script,
                    y = "55%",
                    width = "85%",
                    height = "40%",
                    x_alignment = "50%",
                    y_alignment = "0%",
                    x_padding = "3 vw",
                    fill_color = "rgba(255,255,255,0.95)",
                    font_family = "Inter",
                    font_weight = "400",
                    font_size_maximum = "5 vmin",
                    font_size_minimum = "2 vmin",
                    background_color = "rgba(0,0,0,0.3)",
                    background_x_padding = "30%",
                    background_y_padding = "30%",
                    background_border_radius = "1 vmin",
                    animations = new object[]
                    {
                        new
                        {
                            type = "text-appear",
                            easing = "linear",
                            split = "word",
                        },
                    },
                },
                // Voiceover (Azure TTS built into Creatomate)
                new
                {
                    type = "audio",
                    provider = $"microsoft voice_id={_voiceName}",
                    source = script,
                },
                // Progress bar
                new
                {
                    type = "shape",
                    track = 3,
                    y = "1%",
                    width = "100%",
                    height = "0.5%",
                    fill_color = "rgba(255,255,255,0.7)",
                    path = "M 0 0 L 100 0 L 100 100 L 0 100 Z",
                    animations = new object[]
                    {
                        new
                        {
                            type = "wipe",
                            x_anchor = "0%",
                            fade = false,
                            easing = "linear",
                        },
                    },
                },
            },
        };
    }

    private async Task<ResultResponse<GeneratedVideo>> PollRenderCompletion(
        string renderId, CancellationToken ct)
    {
        const int maxAttempts = 60;
        const int pollIntervalMs = 5000;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            await Task.Delay(pollIntervalMs, ct);

            var response = await _httpClient.GetAsync($"renders/{renderId}", ct);
            if (!response.IsSuccessStatusCode) continue;

            var render = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var status = render.GetProperty("status").GetString();

            switch (status)
            {
                case "succeeded":
                {
                    var url = render.GetProperty("url").GetString()!;
                    var duration = render.TryGetProperty("duration", out var durEl)
                        ? (int)Math.Ceiling(durEl.GetDouble())
                        : 30;

                    var videoBytes = await _httpClient.GetByteArrayAsync(url, ct);

                    _logger.LogInformation("Video render completed: {RenderId} ({Duration}s)", renderId, duration);
                    return ResultResponse<GeneratedVideo>.Success(
                        new GeneratedVideo(videoBytes, duration, "video/mp4"));
                }
                case "failed":
                {
                    var error = render.TryGetProperty("error_message", out var errEl)
                        ? errEl.GetString()
                        : "Unknown render error";
                    return ResultResponse<GeneratedVideo>.Failure($"Render failed: {error}");
                }
            }
        }

        return ResultResponse<GeneratedVideo>.Failure("Render timed out after 5 minutes");
    }
}
