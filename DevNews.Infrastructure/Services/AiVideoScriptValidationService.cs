using System.Text;
using System.Text.Json;
using DevNews.Application.Common.Models;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;

namespace DevNews.Infrastructure.Services;

public class AiVideoScriptValidationService(IAiService aiService) : IVideoScriptValidationService
{
    public async Task<ResultResponse<ScriptValidationResult>> ValidateScriptAsync(
        string script,
        string originalSummary,
        CancellationToken ct = default)
    {
        try
        {
            var prompt = BuildPrompt(script, originalSummary);

            var aiResponse = await aiService.GenerateAsync(prompt, ct: ct);
            if (!aiResponse.IsSuccess || string.IsNullOrWhiteSpace(aiResponse.Data))
                return ResultResponse<ScriptValidationResult>.Failure(aiResponse.ErrorMessage);

            return ParseResponse(aiResponse.Data);
        }
        catch (Exception ex)
        {
            return ResultResponse<ScriptValidationResult>.Failure($"Script validation failed: {ex.Message}");
        }
    }

    private static string BuildPrompt(string script, string originalSummary)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a quality assurance reviewer for short-form developer news videos.");
        sb.AppendLine("Validate this video narration script against the original article summary.");
        sb.AppendLine();
        sb.AppendLine("Check for:");
        sb.AppendLine("- Factual accuracy: script must not add or distort facts from the original summary");
        sb.AppendLine("- Appropriate length: 200-800 characters for a ~30 second read");
        sb.AppendLine("- Tone: professional but engaging, suitable for developer audience");
        sb.AppendLine("- No hallucinated details not in the original summary");
        sb.AppendLine("- No clickbait or sensationalism");
        sb.AppendLine();
        sb.AppendLine($"Original Summary:\n{originalSummary}");
        sb.AppendLine();
        sb.AppendLine($"Video Script:\n{script}");
        sb.AppendLine();
        sb.AppendLine("Return JSON only:");
        sb.AppendLine("{");
        sb.AppendLine("  \"isValid\": true/false,");
        sb.AppendLine("  \"reason\": \"explanation if invalid, null if valid\",");
        sb.AppendLine("  \"qualityScore\": 0-100");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static ResultResponse<ScriptValidationResult> ParseResponse(string jsonResponse)
    {
        try
        {
            var cleaned = JsonResponseHelper.CleanJsonResponse(jsonResponse);

            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            var isValid = root.TryGetProperty("isValid", out var validEl) && validEl.GetBoolean();

            string? reason = null;
            if (root.TryGetProperty("reason", out var reasonEl) && reasonEl.ValueKind != JsonValueKind.Null)
                reason = reasonEl.GetString();

            var qualityScore = root.TryGetProperty("qualityScore", out var scoreEl) ? scoreEl.GetInt32() : 0;

            return ResultResponse<ScriptValidationResult>.Success(
                new ScriptValidationResult(isValid, reason, qualityScore));
        }
        catch (Exception ex)
        {
            return ResultResponse<ScriptValidationResult>.Failure($"Failed to parse validation response: {ex.Message}");
        }
    }
}
