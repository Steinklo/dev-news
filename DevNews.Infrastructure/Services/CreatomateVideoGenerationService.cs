using System.Net.Http.Json;
using System.Text.Json;
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
    private readonly string _templateId;

    public CreatomateVideoGenerationService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<CreatomateVideoGenerationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["CreatomateApiKey"]
            ?? throw new InvalidOperationException("CreatomateApiKey is not configured");
        _templateId = configuration["VideoGeneration:CreatomateTemplateId"]
            ?? throw new InvalidOperationException("VideoGeneration:CreatomateTemplateId is not configured");

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
            _logger.LogDebug("Requesting video render from Creatomate for: {Title}", title);

            var requestBody = new
            {
                template_id = _templateId,
                modifications = new
                {
                    Title = new { text = title },
                    Script = new { text = script }
                }
            };

            var response = await _httpClient.PostAsJsonAsync("renders", requestBody, ct);

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

            // Poll for render completion
            var videoResult = await PollRenderCompletion(renderId, ct);
            return videoResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Video generation failed for: {Title}", title);
            return ResultResponse<GeneratedVideo>.Failure($"Video generation failed: {ex.Message}");
        }
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

                    // Download the video
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
