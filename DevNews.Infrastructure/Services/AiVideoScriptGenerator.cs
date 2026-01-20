using System.Text.Json;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using Microsoft.Extensions.Logging;

namespace DevNews.Infrastructure.Services;

/// <summary>
/// AI-powered video script generator using Claude.
/// Generates educational tutorial-style scripts for developer-focused short-form content.
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

            _logger.LogInformation("Generating educational tutorial script for: {Title}", title);

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

        return $@"You are a professional developer educator creating short-form tutorial content (TikTok/Instagram Reels/YouTube Shorts).

Generate a {targetDurationSeconds}-second educational tutorial script for this developer topic:

**Title**: {title}
**Summary**: {summary}
**Category**: {category}

**Educational Tutorial Requirements**:
- Start with a problem or question developers face (""Want to X? Here's how."")
- Use methodical, step-by-step pacing - NOT fast news delivery
- Maximum {maxWords} words total (150 words/min speaking rate)
- Structure MUST follow this flow:
  1. Problem/Hook (5 sec): State the problem developers have
  2. Solution Steps (main): Walk through the solution step-by-step
  3. Demo/Example: Give a concrete example
  4. Takeaway + CTA: ""Try this in your project today"" or ""Follow for more tutorials""

**Style Guidelines**:
- Educational tone, like a senior dev explaining to a colleague
- Use ""you"" and ""your"" - speak directly to the viewer
- Include actionable phrases: ""First, you'll want to..."", ""The key here is..."", ""Try this yourself...""
- NO marketing fluff, buzzwords, or hype
- NO timestamps or stage directions - just the spoken words
- Content must be accurate and based on the summary provided

**Output Format**:
Return ONLY valid JSON (no markdown formatting):
{{
  ""script"": ""your script text here as one continuous paragraph""
}}

**Example Good Tutorial Script**:
""Want to speed up your Python code without rewriting everything? Here's how with Python 3.13's new JIT compiler. First, upgrade to 3.13 using pyenv or your package manager. The JIT automatically kicks in for tight loops and hot code paths. You don't need to change any code - just run your existing scripts. In my tests, CPU-bound tasks ran 40% faster out of the box. Try this on your slowest script today and see the difference. Follow for more Python performance tips.""

Now generate the educational tutorial script.";
    }

    private class ScriptResponse
    {
        public string? Script { get; set; }
    }
}
