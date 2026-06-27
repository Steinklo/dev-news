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

    // Creatomate render constants. VoiceoverElementName couples the audio element's `name` to the
    // caption element's `transcript_source` — they MUST stay equal or the captions silently break.
    private const string VoiceoverElementName = "Voiceover";
    private const string FontFamily = "Inter";
    private const string BackgroundColor = "#0a0a12";
    private const string TextColor = "#ffffff";

    // Animated backdrop: two large, semi-transparent colour washes that slowly scale so the dark
    // background breathes instead of sitting as a flat fill. Uses solid rgba fills + a "scale"
    // animation with linear easing — all confirmed against the Creatomate docs. No external image
    // call. (Creatomate does NOT accept CSS linear-/radial-gradient strings in fill_color.)
    private const string GlowColorA = "rgba(99,91,255,0.14)";
    private const string GlowColorB = "rgba(40,180,200,0.10)";
    private const string FullFramePath = "M 0 0 L 100 0 L 100 100 L 0 100 Z";

    public CreatomateVideoGenerationService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<CreatomateVideoGenerationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        // Creatomate (video render) is optional — a missing key must NOT throw; we skip rendering
        // in GenerateVideoAsync instead so the rest of the pipeline keeps running.
        _apiKey = configuration["CreatomateApiKey"] ?? "";
        // OpenAI TTS voice id (alloy, echo, fable, onyx, nova, shimmer). Must match the provider in BuildVideoSource.
        _voiceName = configuration["VideoGeneration:TtsVoiceName"] ?? "onyx";

        _httpClient.BaseAddress = new Uri("https://api.creatomate.com/v1/");
        if (!string.IsNullOrWhiteSpace(_apiKey))
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    public async Task<ResultResponse<GeneratedVideo>> GenerateVideoAsync(
        string script,
        string title,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogInformation("Video render skipped — CreatomateApiKey is not configured");
            return ResultResponse<GeneratedVideo>.Failure("CreatomateApiKey is not configured");
        }

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
        return new
        {
            output_format = "mp4",
            width = 1080,
            height = 1920,
            elements = new object[]
            {
                // Solid dark backdrop (replaces the removed DALL-E image — no external call)
                new
                {
                    type = "shape",
                    width = "100%",
                    height = "100%",
                    fill_color = BackgroundColor,
                    path = FullFramePath,
                },
                // Two soft colour washes that slowly scale to give the backdrop subtle motion.
                // Oversized + off-centre so their hard edges sit outside the visible frame.
                new
                {
                    type = "shape",
                    width = "130%",
                    height = "55%",
                    x = "25%",
                    y = "22%",
                    fill_color = GlowColorA,
                    path = FullFramePath,
                    animations = new object[]
                    {
                        new { type = "scale", fade = false, easing = "linear", start_scale = "100%", end_scale = "135%" },
                    },
                },
                new
                {
                    type = "shape",
                    width = "130%",
                    height = "55%",
                    x = "78%",
                    y = "82%",
                    fill_color = GlowColorB,
                    path = FullFramePath,
                    animations = new object[]
                    {
                        new { type = "scale", fade = false, easing = "linear", start_scale = "130%", end_scale = "100%" },
                    },
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
                    fill_color = TextColor,
                    font_family = FontFamily,
                    font_weight = "800",
                    font_size = "8 vmin",
                    animations = new object[]
                    {
                        new { type = "slide", direction = "0°", duration = "1 s" },
                    },
                },
                // Auto-generated captions, synced to the voiceover (replaces the static script block).
                // 'text' is a placeholder; Creatomate fills captions from transcript_source.
                new
                {
                    type = "text",
                    track = 2,
                    text = " ",
                    transcript_source = VoiceoverElementName,
                    transcript_effect = "highlight",
                    transcript_split = "word",
                    transcript_maximum_length = 24,
                    y = "75%",
                    width = "85%",
                    x_alignment = "50%",
                    y_alignment = "50%",
                    fill_color = TextColor,
                    font_family = FontFamily,
                    font_weight = "700",
                    font_size = "6 vmin",
                    background_color = "rgba(0,0,0,0.3)",
                    background_x_padding = "30%",
                    background_y_padding = "30%",
                    background_border_radius = "1 vmin",
                },
                // Voiceover (OpenAI TTS via Creatomate) — named so captions can reference it.
                // Creatomate dropped the "microsoft" (Azure) provider; supported values are now
                // "openai", "elevenlabs", "stabilityai". _voiceName must be valid for this provider.
                new
                {
                    name = VoiceoverElementName,
                    type = "audio",
                    provider = $"openai model_id=tts-1 voice_id={_voiceName}",
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
