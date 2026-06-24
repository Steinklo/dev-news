using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DevNews.Application.Common.Services;
using DevNews.Application.SocialPost.Dtos;
using DevNews.Domain.Common;
using DevNews.Domain.SocialPost.ValueObjects;

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

            var parsed = ParseSocialPostResponse(aiResponse.Data);
            if (!parsed.IsSuccess)
                return parsed;

            // Safety net: the model occasionally overshoots — trim to fit so we never drop a post
            // purely for length (keeps the trailing URL intact).
            return ResultResponse<string>.Success(TruncateToFit(parsed.Data!, SocialPostText.MaxLength));
        }
        catch (Exception ex)
        {
            return ResultResponse<string>.Failure($"Social post generation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Ensures the post is at most <paramref name="max"/> characters, preserving a trailing URL.
    /// </summary>
    internal static string TruncateToFit(string text, int max)
    {
        text = text.Trim();
        if (text.Length <= max)
            return text;

        var match = Regex.Match(text, @"https?://\S+");
        if (match.Success)
        {
            var url = match.Value;
            var body = (text[..match.Index] + text[(match.Index + match.Length)..]).Trim();
            var budget = max - url.Length - 2; // room for a space + ellipsis
            if (budget <= 0)
                return url.Length <= max ? url : url[..max];

            var trimmedBody = body.Length > budget ? body[..budget].TrimEnd() + "…" : body;
            return $"{trimmedBody} {url}";
        }

        return text[..(max - 1)].TrimEnd() + "…";
    }

    private static string BuildSocialPostPrompt(SocialPostEligibleItem item)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Write a short, engaging social media post about this developer news article.");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- A short, punchy hook about why it matters (no need to repeat the full title)");
        sb.AppendLine("- Include the source URL at the end");
        sb.AppendLine("- Keep the ENTIRE post under 280 characters, including the URL. Plain text.");
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
