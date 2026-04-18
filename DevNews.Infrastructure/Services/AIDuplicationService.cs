using System.Text;
using System.Text.Json;
using DevNews.Application.Common.Repositories;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using DevNews.Application.Common.Models;
using Microsoft.Extensions.Logging;

namespace DevNews.Infrastructure.Services;

/// <summary>
/// Deduplication service using AI semantic check to catch rephrased duplicates from different sources.
/// Note: Exact URL matching is handled earlier in the crawl service.
/// </summary>
public class AiDuplicationService(
    IAiService aiService,
    INewsItemRepository repository,
    ILogger<AiDuplicationService> logger) : IDuplicationService
{
    public async Task<ResultResponse<bool>> IsDuplicateAsync(CleanedArticle article, CancellationToken ct = default)
    {
        try
        {
            // AI semantic check - catches same story from different sources
            var articleDate = article.PublishedAt ?? DateTimeOffset.UtcNow;
            var startOfMonth = new DateTimeOffset(articleDate.Year, articleDate.Month, 1, 0, 0, 0, TimeSpan.Zero);
            var endOfMonth = startOfMonth.AddMonths(1);

            var sameMonthArticles = await repository.GetByCategoryAndMonthAsync(
                article.Category,
                startOfMonth,
                endOfMonth,
                limit: 100,
                ct);

            if (!sameMonthArticles.IsSuccess || !sameMonthArticles.Data!.Any())
            {
                return ResultResponse<bool>.Success(false);
            }

            var promptResult = BuildDuplicationCheckPrompt(article, sameMonthArticles.Data!);
            if (!promptResult.IsSuccess)
            {
                return ResultResponse<bool>.Failure(promptResult.ErrorMessage);
            }

            var aiResponse = await aiService.GenerateAsync(promptResult.Data!, ct: ct);
            if (!aiResponse.IsSuccess || string.IsNullOrWhiteSpace(aiResponse.Data))
            {
                // If AI fails, fail open (assume not duplicate)
                logger.LogWarning("AI deduplication check failed, assuming not duplicate: {Title}", article.Title);
                return ResultResponse<bool>.Success(false);
            }

            var parseResult = ParseAiResponse(aiResponse.Data);
            if (!parseResult.IsSuccess)
            {
                return ResultResponse<bool>.Success(false);
            }

            if (parseResult.Data)
            {
                logger.LogDebug("Duplicate detected via AI semantic check: {Title}", article.Title);
            }

            return ResultResponse<bool>.Success(parseResult.Data);
        }
        catch (Exception ex)
        {
            return ResultResponse<bool>.Failure($"Duplication check failed: {ex.Message}");
        }
    }

    private ResultResponse<string> BuildDuplicationCheckPrompt(CleanedArticle article,
        IEnumerable<Domain.NewsItem.NewsItem> recentArticles)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are an expert at identifying duplicate news articles.");
            sb.AppendLine();
            sb.AppendLine("New Article to check:");
            sb.AppendLine($"Title: {article.Title}");
            sb.AppendLine($"Summary: {article.Summary}");
            sb.AppendLine($"Category: {article.Category}");
            sb.AppendLine($"URL: {article.Url}");
            sb.AppendLine();
            sb.AppendLine($"Compare against these articles from the same category ({article.Category}) and month:");
            sb.AppendLine();

            var count = 1;
            foreach (var existing in recentArticles)
            {
                sb.AppendLine($"Article {count}:");
                sb.AppendLine($"  Title: {existing.Title.Value}");
                sb.AppendLine($"  Summary: {existing.Summary.Value}");
                sb.AppendLine($"  URL: {existing.Url.Value}");
                sb.AppendLine();
                count++;
            }

            sb.AppendLine("Determine if the new article is a duplicate or near-duplicate of any existing article.");
            sb.AppendLine("Consider:");
            sb.AppendLine("- Same or very similar topic/subject matter");
            sb.AppendLine("- Same news event or announcement");
            sb.AppendLine("- Different source but same story");
            sb.AppendLine();
            sb.AppendLine("Return JSON only (no prose, no markdown):");
            sb.AppendLine("{");
            sb.AppendLine("  \"isDuplicate\": true or false,");
            sb.AppendLine("  \"reason\": \"brief explanation\"");
            sb.AppendLine("}");

            return ResultResponse<string>.Success(sb.ToString());
        }
        catch (Exception ex)
        {
            return ResultResponse<string>.Failure($"Failed to build duplication check prompt: {ex.Message}");
        }
    }

    private ResultResponse<bool> ParseAiResponse(string jsonResponse)
    {
        try
        {
            var cleaned = JsonResponseHelper.CleanJsonResponse(jsonResponse);

            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            if (!root.TryGetProperty("isDuplicate", out var isDuplicateElement))
            {
                return ResultResponse<bool>.Failure("AI response missing 'isDuplicate' field");
            }

            return ResultResponse<bool>.Success(isDuplicateElement.GetBoolean());
        }
        catch (Exception ex)
        {
            return ResultResponse<bool>.Failure($"Failed to parse AI response: {ex.Message}");
        }
    }
}