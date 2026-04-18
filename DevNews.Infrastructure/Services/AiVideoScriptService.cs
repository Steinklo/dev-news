using System.Text;
using System.Text.Json;
using DevNews.Application.Common.Services;
using DevNews.Application.Common.Models;
using DevNews.Domain.Common;

namespace DevNews.Infrastructure.Services;

public class AiVideoScriptService(IAiService aiService) : IVideoScriptService
{
    public async Task<ResultResponse<string>> GenerateScriptAsync(
        string title,
        string summary,
        string category,
        CancellationToken ct = default)
    {
        try
        {
            var prompt = BuildPrompt(title, summary, category);

            var aiResponse = await aiService.GenerateAsync(prompt, ct: ct);
            if (!aiResponse.IsSuccess || string.IsNullOrWhiteSpace(aiResponse.Data))
                return ResultResponse<string>.Failure(aiResponse.ErrorMessage);

            var script = ParseResponse(aiResponse.Data);
            return script;
        }
        catch (Exception ex)
        {
            return ResultResponse<string>.Failure($"Script generation failed: {ex.Message}");
        }
    }

    public async Task<ResultResponse<string>> GenerateDailyVideoScriptAsync(
        IReadOnlyList<NewsArticleSummary> items,
        CancellationToken ct = default)
    {
        try
        {
            var prompt = BuildDailyVideoScriptPrompt(items);

            var aiResponse = await aiService.GenerateAsync(prompt, ct: ct);
            if (!aiResponse.IsSuccess || string.IsNullOrWhiteSpace(aiResponse.Data))
                return ResultResponse<string>.Failure(aiResponse.ErrorMessage);

            return ParseResponse(aiResponse.Data);
        }
        catch (Exception ex)
        {
            return ResultResponse<string>.Failure($"Video script generation failed: {ex.Message}");
        }
    }

    private static string BuildPrompt(string title, string summary, string category)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an expert short-form video scriptwriter for a developer news channel.");
        sb.AppendLine("Write a concise, engaging narration script for a 30-second video about this news item.");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- Script must be 200-800 characters (30-second read at natural pace)");
        sb.AppendLine("- Start with a hook that grabs attention in the first sentence");
        sb.AppendLine("- Use clear, developer-friendly language — no buzzwords or hype");
        sb.AppendLine("- Include the key facts: what happened, why it matters to developers");
        sb.AppendLine("- End with a brief takeaway or call-to-action");
        sb.AppendLine("- Plain text only — no markdown, no formatting, no emojis");
        sb.AppendLine("- Write as if speaking to the viewer directly");
        sb.AppendLine();
        sb.AppendLine($"Title: {title}");
        sb.AppendLine($"Category: {category}");
        sb.AppendLine($"Summary: {summary}");
        sb.AppendLine();
        sb.AppendLine("Return JSON only:");
        sb.AppendLine("{");
        sb.AppendLine("  \"isSuccess\": true,");
        sb.AppendLine("  \"script\": \"the narration script text\"");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string BuildDailyVideoScriptPrompt(IReadOnlyList<NewsArticleSummary> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Write a 30-second narration script summarizing today's top AI developer news.");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- Cover the key highlights from all articles in a cohesive narrative");
        sb.AppendLine("- 200-800 characters, spoken tone");
        sb.AppendLine("- Start with an attention-grabbing hook");
        sb.AppendLine("- End with a key takeaway");
        sb.AppendLine("- No emojis, no markdown");
        sb.AppendLine();
        sb.AppendLine("Articles:");

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            sb.AppendLine($"{i + 1}. Title: {item.Title}");
            sb.AppendLine($"   Category: {item.Category}");
            sb.AppendLine($"   Summary: {item.Summary}");
            sb.AppendLine();
        }

        sb.AppendLine("Return JSON only:");
        sb.AppendLine("{");
        sb.AppendLine("  \"isSuccess\": true,");
        sb.AppendLine("  \"script\": \"the full narration script\"");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static ResultResponse<string> ParseResponse(string jsonResponse)
    {
        try
        {
            var cleaned = JsonResponseHelper.CleanJsonResponse(jsonResponse);

            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            if (!root.TryGetProperty("isSuccess", out var successEl) || !successEl.GetBoolean())
                return ResultResponse<string>.Failure("AI returned unsuccessful response for script generation");

            if (!root.TryGetProperty("script", out var scriptEl))
                return ResultResponse<string>.Failure("Missing 'script' field in AI response");

            var script = scriptEl.GetString();
            if (string.IsNullOrWhiteSpace(script))
                return ResultResponse<string>.Failure("Script is empty in AI response");

            return ResultResponse<string>.Success(script);
        }
        catch (Exception ex)
        {
            return ResultResponse<string>.Failure($"Failed to parse script response: {ex.Message}");
        }
    }
}
