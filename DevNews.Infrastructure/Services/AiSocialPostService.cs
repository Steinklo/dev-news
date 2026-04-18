using System.Text;
using System.Text.Json;
using DevNews.Application.Common.Services;
using DevNews.Application.SocialPost.Dtos;
using DevNews.Domain.Common;

namespace DevNews.Infrastructure.Services;

public class AiSocialPostService(IAiService aiService) : ISocialPostGenerationService
{
    public async Task<ResultResponse<string>> GenerateSocialPostAsync(
        SocialPostEligibleItem item,
        CancellationToken ct = default)
    {
        try
        {
            var prompt = BuildSocialPostPrompt(item);

            var aiResponse = await aiService.GenerateAsync(prompt, ct: ct);
            if (!aiResponse.IsSuccess || string.IsNullOrWhiteSpace(aiResponse.Data))
                return ResultResponse<string>.Failure(aiResponse.ErrorMessage);

            return ParseSocialPostResponse(aiResponse.Data);
        }
        catch (Exception ex)
        {
            return ResultResponse<string>.Failure($"Social post generation failed: {ex.Message}");
        }
    }

    private static string BuildSocialPostPrompt(SocialPostEligibleItem item)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Write a short, engaging social media post about this developer news article.");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- Include the article title and a 1-2 sentence hook about why it matters");
        sb.AppendLine("- Include the source URL at the end");
        sb.AppendLine("- 200-500 characters total, plain text");
        sb.AppendLine("- No emojis, no markdown");
        sb.AppendLine("- Professional but engaging tone");
        sb.AppendLine();
        sb.AppendLine($"Title: {item.Title}");
        sb.AppendLine($"Category: {item.Category}");
        sb.AppendLine($"Summary: {item.Summary}");
        sb.AppendLine($"Source URL: {item.SourceUrl}");
        sb.AppendLine();
        sb.AppendLine("Return JSON only:");
        sb.AppendLine("{");
        sb.AppendLine("  \"isSuccess\": true,");
        sb.AppendLine("  \"postText\": \"the full social media post text\"");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static ResultResponse<string> ParseSocialPostResponse(string jsonResponse)
    {
        try
        {
            var cleaned = JsonResponseHelper.CleanJsonResponse(jsonResponse);

            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            if (!root.TryGetProperty("isSuccess", out var successEl) || !successEl.GetBoolean())
                return ResultResponse<string>.Failure("AI returned unsuccessful response for social post generation");

            if (!root.TryGetProperty("postText", out var postEl))
                return ResultResponse<string>.Failure("Missing 'postText' field in AI response");

            var postText = postEl.GetString();
            if (string.IsNullOrWhiteSpace(postText))
                return ResultResponse<string>.Failure("Post text is empty in AI response");

            return ResultResponse<string>.Success(postText);
        }
        catch (Exception ex)
        {
            return ResultResponse<string>.Failure($"Failed to parse social post response: {ex.Message}");
        }
    }
}
