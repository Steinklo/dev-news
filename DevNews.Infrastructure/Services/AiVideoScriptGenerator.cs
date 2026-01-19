using System.Text.Json;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using Microsoft.Extensions.Logging;

namespace DevNews.Infrastructure.Services;

/// <summary>
/// AI-powered video script generator using Claude.
/// </summary>
public class AiVideoScriptGenerator : IVideoScriptGenerator
{
    private readonly IAiService _aiService;
    private readonly ILogger<AiVideoScriptGenerator> _logger;

    public AiVideoScriptGenerator(
        IAiService aiService,
        ILogger<AiVideoScriptGenerator> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    public async Task<ResultResponse<string>> GenerateScriptAsync(
        string title,
        string summary,
        string category,
        int targetDurationSeconds,
        CancellationToken ct = default)
    {
        try
        {
            var prompt = BuildScriptPrompt(title, summary, category, targetDurationSeconds);

            _logger.LogInformation("Generating video script for: {Title}", title);

            var result = await _aiService.GenerateAsync(prompt, ct);

            if (!result.IsSuccess)
            {
                _logger.LogError("AI service failed to generate script: {Error}", result.ErrorMessage);
                return ResultResponse<string>.Failure(result.ErrorMessage ?? "AI service failed");
            }

            // Parse JSON response: { "script": "..." }
            var scriptResponse = JsonSerializer.Deserialize<ScriptResponse>(result.Data!);

            if (scriptResponse?.Script == null)
            {
                _logger.LogError("Failed to parse script from AI response");
                return ResultResponse<string>.Failure("Invalid script response format");
            }

            _logger.LogInformation("Successfully generated video script ({Words} words)",
                scriptResponse.Script.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);

            return ResultResponse<string>.Success(scriptResponse.Script);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating video script");
            return ResultResponse<string>.Failure($"Script generation failed: {ex.Message}");
        }
    }

    private static string BuildScriptPrompt(
        string title,
        string summary,
        string category,
        int targetDurationSeconds)
    {
        var maxWords = (int)(targetDurationSeconds * 2.5); // ~150 words/min speaking rate

        return $@"You are a professional video script writer for developer-focused short-form content (TikTok/Instagram Reels/YouTube Shorts).

Generate a {targetDurationSeconds}-second video script for this developer news:

**Title**: {title}
**Summary**: {summary}
**Category**: {category}

**Requirements**:
- Hook in first 3 seconds (critical for retention) - start with a question, surprising fact, or bold statement
- Maximum {maxWords} words total (150 words/min speech rate)
- Developer-friendly language, no marketing fluff or buzzwords
- Conversational tone: energetic but professional
- Structure: Hook → Core Info → Call-to-action
- Call-to-action at end: ""Follow for more dev news"" or similar
- NO timestamps or stage directions - just the spoken words
- Content must be accurate and based on the summary

**Output Format**:
Return ONLY valid JSON (no markdown formatting):
{{
  ""script"": ""your script text here as one continuous paragraph""
}}

**Example Good Script**:
""Did you know Python 3.13 just dropped with a 40% performance boost? The new JIT compiler uses copy-and-patch optimization, making tight loops significantly faster. This is the biggest performance jump since Python 3.11. If you're doing data science or backend work, upgrade now. Follow for more Python updates.""

Now generate the script for the news above.";
    }

    private class ScriptResponse
    {
        public string? Script { get; set; }
    }
}
