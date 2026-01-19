using System.Text.Json;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using Microsoft.Extensions.Logging;

namespace DevNews.Infrastructure.Services;

/// <summary>
/// AI-powered video validation service using Claude with vision capabilities.
/// </summary>
public class AiVideoValidationService : IVideoValidationService
{
    private readonly IAiService _aiService;
    private readonly ILogger<AiVideoValidationService> _logger;

    public AiVideoValidationService(
        IAiService aiService,
        ILogger<AiVideoValidationService> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    public async Task<ResultResponse<(bool IsValid, string Reason)>> ValidateVideoAsync(
        string videoUrl,
        string originalScript,
        CancellationToken ct = default)
    {
        try
        {
            var prompt = BuildValidationPrompt(videoUrl, originalScript);

            _logger.LogInformation("Validating video: {VideoUrl}", videoUrl);

            var result = await _aiService.GenerateAsync(prompt, ct);

            if (!result.IsSuccess)
            {
                _logger.LogError("AI service failed to validate video: {Error}", result.ErrorMessage);
                return ResultResponse<(bool, string)>.Failure(result.ErrorMessage ?? "AI validation service failed");
            }

            // Parse JSON response: { "isValid": true/false, "reason": "..." }
            var validationResponse = JsonSerializer.Deserialize<ValidationResponse>(result.Data!);

            if (validationResponse == null)
            {
                _logger.LogError("Failed to parse validation response from AI");
                return ResultResponse<(bool, string)>.Failure("Invalid validation response format");
            }

            _logger.LogInformation("Video validation result: {IsValid} - {Reason}",
                validationResponse.IsValid, validationResponse.Reason);

            return ResultResponse<(bool, string)>.Success((validationResponse.IsValid, validationResponse.Reason ?? "Unknown"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during video validation");
            return ResultResponse<(bool, string)>.Failure($"Validation failed: {ex.Message}");
        }
    }

    private static string BuildValidationPrompt(string videoUrl, string script)
    {
        return $@"You are a quality control specialist for developer-focused short-form video content (TikTok/Instagram/YouTube Shorts).

Your task is to validate the video at this URL and determine if it meets quality standards for publication.

**Video URL**: {videoUrl}
**Original Script**: {script}

**Validation Criteria** (ALL must pass):

1. **Audio Quality**: Clear speech, no distortion, professional voice quality
2. **Pacing**: Speech rate is natural (not too fast/slow), matches script length
3. **Visual Quality**: No artifacts, proper resolution (720p+), good contrast
4. **Content Accuracy**: Video content matches the script's key points
5. **Duration**: Video is 15-60 seconds long
6. **Professionalism**: No inappropriate content, suitable for professional audience
7. **Technical**: No corrupted frames, proper encoding, plays smoothly

**Decision Rules**:
- If ANY criteria fails → set isValid to FALSE
- Provide specific reason for rejection (which criteria failed and why)
- If all criteria pass → set isValid to TRUE with brief confirmation

**Output Format**:
Return ONLY valid JSON (no markdown formatting):
{{
  ""isValid"": true,
  ""reason"": ""Brief explanation of decision (1-2 sentences)""
}}

**Example Rejection**:
{{
  ""isValid"": false,
  ""reason"": ""Audio quality is poor with significant background noise making speech difficult to understand. Pacing is too fast (script completed in 8 seconds instead of target 30 seconds).""
}}

**Example Approval**:
{{
  ""isValid"": true,
  ""reason"": ""Video meets all quality criteria: clear audio, proper pacing, good visual quality, and accurately represents the script content.""
}}

Now validate the video.";
    }

    private class ValidationResponse
    {
        public bool IsValid { get; set; }
        public string? Reason { get; set; }
    }
}
