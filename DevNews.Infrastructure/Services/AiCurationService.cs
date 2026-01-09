using System.Text;
using System.Text.Json;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using DevNews.Domain.Common.Models;

namespace DevNews.Infrastructure.Services;

public class AiCurationService : ICurationService
{
    private readonly IAiService _aiService;

    public AiCurationService(IAiService aiService)
    {
        _aiService = aiService;
    }

    public async Task<ResultResponse<CleanedArticle>> CurateAsync(CrawledArticle article, CancellationToken ct = default)
    {
        try
        {
            // Build the AI prompt
            var promptResult = BuildPrompt(article.Html, article.Url.ToString());
            if (!promptResult.IsSuccess)
            {
                return ResultResponse<CleanedArticle>.Failure(promptResult.ErrorMessage);
            }

            // Call AI service to extract article data
            var aiResponse = await _aiService.GenerateAsync(promptResult.Data, ct);
            if (!aiResponse.IsSuccess || string.IsNullOrWhiteSpace(aiResponse.Data))
            {
                return ResultResponse<CleanedArticle>.Failure(aiResponse.ErrorMessage);
            }

            // Parse the AI response
            var parseResult = ParseAiResponse(aiResponse.Data, article.Url);
            if (!parseResult.IsSuccess)
            {
                return ResultResponse<CleanedArticle>.Failure(parseResult.ErrorMessage);
            }

            return ResultResponse<CleanedArticle>.Success(parseResult.Data);
        }
        catch (Exception ex)
        {
            return ResultResponse<CleanedArticle>.Failure($"Curation failed: {ex.Message}");
        }
    }

    private ResultResponse<string> BuildPrompt(string articleHtml, string articleUrl)
    {
        try
        {
            var categoriesList = string.Join(", ", CurationRules.AllowedCategories.Select(c => c.ToString()));

            var sb = new StringBuilder();
            sb.AppendLine("You are an expert developer news curator.");
            sb.AppendLine();
            sb.AppendLine("Extract and curate the following article from HTML:");
            sb.AppendLine($"Article URL: {articleUrl}");
            sb.AppendLine();
            sb.AppendLine("Extract the following fields:");
            sb.AppendLine("- title: The article title (clean, factual)");
            sb.AppendLine("- summary: A TL;DR summary of the article content");
            sb.AppendLine("- category: The best-fit category");
            sb.AppendLine("- publishedAt: The publication date in ISO 8601 format (if available, otherwise null)");
            sb.AppendLine("- author: The author name (if available, otherwise null)");
            sb.AppendLine();
            sb.AppendLine("Strict rules:");
            sb.AppendLine("- Only content clearly relevant for software developers (reject marketing, HR, business-only, personal blogs without technical depth)");
            sb.AppendLine($"- Title must be {CurationRules.MinTitleLength}–{CurationRules.MaxTitleLength} characters and factual");
            sb.AppendLine($"- Summary (TL;DR) must be {CurationRules.MinSummaryLength}–{CurationRules.MaxSummaryLength} characters, concise, spoiler-free");
            sb.AppendLine($"- Category must be exactly one of: {categoriesList}");
            sb.AppendLine("- Reject ads, clickbait, low-depth posts, paywalled content summaries");
            sb.AppendLine();
            sb.AppendLine("Return JSON only (no prose, no markdown).");
            sb.AppendLine("Success case:");
            sb.AppendLine("{");
            sb.AppendLine("  \"isSuccess\": true,");
            sb.AppendLine("  \"data\": {");
            sb.AppendLine("    \"title\": \"...\",");
            sb.AppendLine("    \"summary\": \"...\",");
            sb.AppendLine("    \"category\": \"...\",");
            sb.AppendLine("    \"publishedAt\": \"2026-01-09T12:00:00Z\" or null,");
            sb.AppendLine("    \"author\": \"...\" or null");
            sb.AppendLine("  },");
            sb.AppendLine("  \"errorMessage\": null");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("Failure case:");
            sb.AppendLine("{");
            sb.AppendLine("  \"isSuccess\": false,");
            sb.AppendLine("  \"data\": null,");
            sb.AppendLine("  \"errorMessage\": \"brief explanation\"");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("Article HTML:");
            sb.Append(articleHtml);

            return ResultResponse<string>.Success(sb.ToString());
        }
        catch (Exception ex)
        {
            return ResultResponse<string>.Failure($"Failed to build prompt: {ex.Message}");
        }
    }

    private ResultResponse<CleanedArticle> ParseAiResponse(string jsonResponse, Uri url)
    {
        try
        {
            // Clean up the response (remove markdown code blocks if present)
            var cleaned = jsonResponse.Trim();
            if (cleaned.StartsWith("```json"))
                cleaned = cleaned.Substring(7);
            if (cleaned.StartsWith("```"))
                cleaned = cleaned.Substring(3);
            if (cleaned.EndsWith("```"))
                cleaned = cleaned.Substring(0, cleaned.Length - 3);
            cleaned = cleaned.Trim();

            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            // Check if AI response indicates success
            if (!root.TryGetProperty("isSuccess", out var isSuccessElement) || !isSuccessElement.GetBoolean())
            {
                var errorMsg = root.TryGetProperty("errorMessage", out var errorElement) 
                    ? errorElement.GetString() ?? "AI returned unsuccessful response"
                    : "AI returned unsuccessful response";
                return ResultResponse<CleanedArticle>.Failure(errorMsg);
            }

            // Get data object
            if (!root.TryGetProperty("data", out var dataElement) || dataElement.ValueKind == JsonValueKind.Null)
            {
                return ResultResponse<CleanedArticle>.Failure("AI response data is null");
            }

            // Extract required fields
            if (!dataElement.TryGetProperty("title", out var titleElement))
            {
                return ResultResponse<CleanedArticle>.Failure("Missing 'title' field in AI response");
            }
            var title = titleElement.GetString();
            if (string.IsNullOrWhiteSpace(title))
            {
                return ResultResponse<CleanedArticle>.Failure("Title is empty in AI response");
            }

            if (!dataElement.TryGetProperty("summary", out var summaryElement))
            {
                return ResultResponse<CleanedArticle>.Failure("Missing 'summary' field in AI response");
            }
            var summary = summaryElement.GetString();
            if (string.IsNullOrWhiteSpace(summary))
            {
                return ResultResponse<CleanedArticle>.Failure("Summary is empty in AI response");
            }

            if (!dataElement.TryGetProperty("category", out var categoryElement))
            {
                return ResultResponse<CleanedArticle>.Failure("Missing 'category' field in AI response");
            }
            var category = categoryElement.GetString();
            if (string.IsNullOrWhiteSpace(category))
            {
                return ResultResponse<CleanedArticle>.Failure("Category is empty in AI response");
            }

            // Extract optional fields
            DateTimeOffset? publishedAt = null;
            if (dataElement.TryGetProperty("publishedAt", out var publishedAtElement) 
                && publishedAtElement.ValueKind != JsonValueKind.Null)
            {
                var publishedAtStr = publishedAtElement.GetString();
                if (!string.IsNullOrWhiteSpace(publishedAtStr) 
                    && DateTimeOffset.TryParse(publishedAtStr, out var parsed))
                {
                    publishedAt = parsed;
                }
            }

            string? author = null;
            if (dataElement.TryGetProperty("author", out var authorElement) 
                && authorElement.ValueKind != JsonValueKind.Null)
            {
                author = authorElement.GetString();
            }

            var cleanedArticle = new CleanedArticle(
                Title: title,
                Summary: summary,
                Category: category,
                Url: url,
                PublishedAt: publishedAt,
                Author: author
            );

            return ResultResponse<CleanedArticle>.Success(cleanedArticle);
        }
        catch (Exception ex)
        {
            return ResultResponse<CleanedArticle>.Failure($"Failed to parse AI response: {ex.Message}");
        }
    }
}

