using System.Net.Http.Headers;
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
        // NOTE: auth is attached per-request (see SendAsync calls), NOT as a default header. The
        // finished render is downloaded from Creatomate's public storage (Backblaze), which returns
        // 401 if any Authorization header is present — a default bearer would leak onto that download.
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

            using var renderRequest = new HttpRequestMessage(HttpMethod.Post, "renders")
            {
                Content = JsonContent.Create(requestBody, options: JsonOptions),
            };
            renderRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            var response = await _httpClient.SendAsync(renderRequest, ct);

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
                // Solid dark backdrop on the lowest track (track = z-order; higher = nearer front).
                // Elements MUST have explicit tracks: a no-track element auto-lands in front and would
                // paint over the text (which is exactly what hid the title/captions before).
                new
                {
                    type = "shape",
                    track = 1,
                    width = "100%",
                    height = "100%",
                    fill_color = BackgroundColor,
                    path = FullFramePath,
                },
                // Title text
                new
                {
                    type = "text",
                    track = 2,
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
                    track = 3,
                    text = " ",
                    transcript_source = VoiceoverElementName,
                    transcript_effect = "highlight",
                    transcript_split = "word",
                    transcript_maximum_length = 24,
                    y = "58%",
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
                    background_border_radius = "8%",
                },
                // Voiceover (OpenAI TTS via Creatomate) — named so captions can reference it.
                // Creatomate dropped the "microsoft" (Azure) provider; supported values are now
                // "openai", "elevenlabs", "stabilityai". _voiceName must be valid for this provider.
                new
                {
                    name = VoiceoverElementName,
                    type = "audio",
                    // OpenAI provider uses `model` and `voice` (NOT ElevenLabs' `model_id`/`voice_id`).
                    // Confirmed field-by-field against the live Creatomate API, which 400s naming each
                    // missing required parameter in turn. Full OpenAI form: "openai model=<m> voice=<v>".
                    provider = $"openai model=tts-1 voice={_voiceName}",
                    source = script,
                },
                // Progress bar
                new
                {
                    type = "shape",
                    track = 4,
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

            using var statusRequest = new HttpRequestMessage(HttpMethod.Get, $"renders/{renderId}");
            statusRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            var response = await _httpClient.SendAsync(statusRequest, ct);
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

                    // No auth header here on purpose: the URL is Creatomate's public storage and
                    // 401s if an Authorization header is sent (see constructor note).
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
