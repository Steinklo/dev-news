using System.Text;
using System.Text.Json;
using DevNews.Application.Common.Repositories;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using DevNews.Domain.Common.Models;

namespace DevNews.Infrastructure.Services;

public class AiDuplicationService : IDuplicationService
{
    private readonly IAiService _aiService;
    private readonly INewsItemRepository _repository;

    public AiDuplicationService(IAiService aiService, INewsItemRepository repository)
    {
        _aiService = aiService;
        _repository = repository;
    }

    public async Task<ResultResponse<bool>> IsDuplicateAsync(CleanedArticle article, CancellationToken ct = default)
    {
        try
        {
            // First check for exact URL match
            var existingByUrl = await _repository.GetByUrlAsync(article.Url.ToString(), ct);
            if (existingByUrl.IsSuccess && existingByUrl.Data != null)
            {
                return ResultResponse<bool>.Success(true);
            }

            // Determine the date to use (PublishedAt or current date)
            var articleDate = article.PublishedAt ?? DateTimeOffset.UtcNow;

            // Get start and end of the month for the article
            var startOfMonth = new DateTimeOffset(articleDate.Year, articleDate.Month, 1, 0, 0, 0, articleDate.Offset);
            var endOfMonth = startOfMonth.AddMonths(1);

            // Get articles from same category and same month/year
            var sameMonthArticles = await _repository.GetByCategoryAndDateRangeAsync(
                article.Category,
                startOfMonth,
                endOfMonth,
                ct);

            if (!sameMonthArticles.IsSuccess || !sameMonthArticles.Data.Any())
            {
                // No articles to compare against, not a duplicate
                return ResultResponse<bool>.Success(false);
            }

            // Build AI prompt to check for semantic duplicates
            var promptResult = BuildDuplicationCheckPrompt(article, sameMonthArticles.Data);
            if (!promptResult.IsSuccess)
            {
                return ResultResponse<bool>.Failure(promptResult.ErrorMessage);
            }

            // Call AI service
            var aiResponse = await _aiService.GenerateAsync(promptResult.Data, ct);
            if (!aiResponse.IsSuccess || string.IsNullOrWhiteSpace(aiResponse.Data))
            {
                // If AI fails, fall back to URL check only
                return ResultResponse<bool>.Success(false);
            }

            // Parse AI response
            var parseResult = ParseAiResponse(aiResponse.Data);
            if (!parseResult.IsSuccess)
            {
                // If parsing fails, assume not duplicate (fail open)
                return ResultResponse<bool>.Success(false);
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