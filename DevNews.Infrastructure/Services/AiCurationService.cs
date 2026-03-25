using System.Text;
using System.Text.Json;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using DevNews.Application.Common.Models;
using DevNews.Domain.NewsItem.Enums;

namespace DevNews.Infrastructure.Services;

public class AiCurationService(IAiService aiService) : ICurationService
{
    /// <summary>
    /// Known source mappings from domain to display name.
    /// </summary>
    private static readonly Dictionary<string, string> SourceMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        // AI Model Providers
        ["openai.com"] = "OpenAI",
        ["anthropic.com"] = "Anthropic",
        ["blog.google"] = "Google AI",
        ["ai.google.dev"] = "Google AI",
        ["deepmind.google"] = "Google DeepMind",
        ["ai.meta.com"] = "Meta AI",
        ["mistral.ai"] = "Mistral AI",
        ["cohere.com"] = "Cohere",

        // AI Developer Platforms
        ["huggingface.co"] = "Hugging Face",
        ["replicate.com"] = "Replicate",
        ["together.ai"] = "Together AI",
        ["fireworks.ai"] = "Fireworks AI",
        ["groq.com"] = "Groq",

        // AI Frameworks & Tools
        ["blog.langchain.dev"] = "LangChain",
        ["langchain.com"] = "LangChain",
        ["llamaindex.ai"] = "LlamaIndex",
        ["docs.crewai.com"] = "CrewAI",

        // AI News & Analysis
        ["simonwillison.net"] = "Simon Willison",
        ["latent.space"] = "Latent Space",
        ["www.latent.space"] = "Latent Space",

        // Cloud / AI Infrastructure
        ["aws.amazon.com"] = "AWS",
        ["cloud.google.com"] = "Google Cloud",
        ["azure.microsoft.com"] = "Microsoft Azure",
        ["nvidia.com"] = "NVIDIA",
        ["developer.nvidia.com"] = "NVIDIA Developer",

        // Dev Platforms
        ["github.com"] = "GitHub",
        ["github.blog"] = "GitHub Blog",
        ["dev.to"] = "DEV Community",
        ["medium.com"] = "Medium",

        // Security
        ["krebsonsecurity.com"] = "Krebs on Security",
        ["bleepingcomputer.com"] = "BleepingComputer",
        ["thehackernews.com"] = "The Hacker News",
        ["cve.mitre.org"] = "CVE",
        ["nvd.nist.gov"] = "NVD",

        // Aggregators
        ["news.ycombinator.com"] = "Hacker News",
        ["ycombinator.com"] = "Hacker News",
        ["lobste.rs"] = "Lobsters",
        ["infoq.com"] = "InfoQ",
        ["reddit.com"] = "Reddit",
        ["old.reddit.com"] = "Reddit",

        // Company Engineering Blogs
        ["engineering.fb.com"] = "Meta Engineering",
        ["devblogs.microsoft.com"] = "Microsoft DevBlogs",
        ["blog.cloudflare.com"] = "Cloudflare",
    };

    private static string ResolveSource(Uri url)
    {
        var host = url.Host.ToLowerInvariant();

        // Try exact match first
        if (SourceMappings.TryGetValue(host, out var source))
            return source;

        // Try without www.
        var hostWithoutWww = host.StartsWith("www.") ? host[4..] : host;
        if (SourceMappings.TryGetValue(hostWithoutWww, out source))
            return source;

        // Try with subdomain removed (e.g., blog.example.com -> example.com)
        var parts = hostWithoutWww.Split('.');
        if (parts.Length > 2)
        {
            var rootDomain = string.Join(".", parts[^2..]);
            if (SourceMappings.TryGetValue(rootDomain, out source))
                return source;
        }

        // Fallback: capitalize the domain name
        var fallback = hostWithoutWww.Split('.')[0];
        return char.ToUpper(fallback[0]) + fallback[1..];
    }

    public async Task<ResultResponse<CleanedArticle>> CurateAsync(CrawledArticle article,
        CancellationToken ct = default)
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
            var aiResponse = await aiService.GenerateAsync(promptResult.Data!, ct);
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

            return ResultResponse<CleanedArticle>.Success(parseResult.Data!);
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
            var severityList = string.Join(", ", Enum.GetNames<SeverityEnum>());

            var sb = new StringBuilder();
            sb.AppendLine("You are an expert AI developer news curator specializing in artificial intelligence, machine learning, and AI engineering content.");
            sb.AppendLine();
            sb.AppendLine("Extract and curate the following article:");
            sb.AppendLine($"Article URL: {articleUrl}");
            sb.AppendLine();
            sb.AppendLine("Extract the following fields:");
            sb.AppendLine("- title: The article title (clean, factual)");
            sb.AppendLine(
                "- summary: A TL;DR summary of the article content (80-160 words, dense, no fluff, developer language)");
            sb.AppendLine("- category: The best-fit category from the allowed list");
            sb.AppendLine("- relevanceScore: 0-100 indicating how relevant this is for professional developers");
            sb.AppendLine("- severity: ONLY for AiSafetyAndSecurity category - one of: " + severityList);
            sb.AppendLine(
                "- tags: Array of max 5 tags for filtering (e.g. cve, kubernetes, go1.24, breaking-change, supply-chain)");
            sb.AppendLine("- publishedAt: The publication date in ISO 8601 format (if available, otherwise null)");
            sb.AppendLine("- author: The author name (if available, otherwise null)");
            sb.AppendLine();
            sb.AppendLine("Strict rules:");
            sb.AppendLine(
                "- Only content clearly relevant for AI developers and engineers (reject general web dev without AI angle, HR, business-only, marketing, or personal blogs without AI/ML technical depth)");
            sb.AppendLine(
                $"- Title must be {CurationRules.MinTitleLength}–{CurationRules.MaxTitleLength} characters and factual");
            sb.AppendLine(
                $"- Summary (TL;DR) must be {CurationRules.MinSummaryLength}–{CurationRules.MaxSummaryLength} characters, concise, spoiler-free");
            sb.AppendLine($"- Category must be exactly one of: {categoriesList}");
            sb.AppendLine(
                "- relevanceScore: 90+ for major model releases/breaking API changes, 70-89 for important framework updates/research, 50-69 for notable community content, below 50 for tangentially-related content");
            sb.AppendLine("- severity is REQUIRED for AiSafetyAndSecurity, must be null for other categories");
            sb.AppendLine("- tags: max 5, lowercase, for search/filtering (e.g. cve, kubernetes, breaking-change)");
            sb.AppendLine("- Reject ads, clickbait, low-depth posts, paywalled content summaries");
            sb.AppendLine();
            sb.AppendLine("Return JSON only (no prose, no markdown).");
            sb.AppendLine("Success case:");
            sb.AppendLine("{");
            sb.AppendLine("  \"isSuccess\": true,");
            sb.AppendLine("  \"data\": {");
            sb.AppendLine("    \"title\": \"...\",");
            sb.AppendLine("    \"summary\": \"...\",");
            sb.AppendLine("    \"category\": \"AiSafetyAndSecurity\",");
            sb.AppendLine("    \"relevanceScore\": 85,");
            sb.AppendLine("    \"severity\": \"High\" or null,");
            sb.AppendLine("    \"tags\": [\"cve\", \"openssl\", \"critical\"],");
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
            sb.AppendLine("Article Content:");
            // Truncate content to ~15k chars (~4k tokens) to stay well under rate limits
            const int maxContentLength = 15000;
            if (articleHtml.Length > maxContentLength)
            {
                sb.Append(articleHtml.AsSpan(0, maxContentLength));
                sb.Append("\n[TRUNCATED]");
            }
            else
            {
                sb.Append(articleHtml);
            }

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

            // Extract title
            if (!dataElement.TryGetProperty("title", out var titleElement))
            {
                return ResultResponse<CleanedArticle>.Failure("Missing 'title' field in AI response");
            }

            var title = titleElement.GetString();
            if (string.IsNullOrWhiteSpace(title))
            {
                return ResultResponse<CleanedArticle>.Failure("Title is empty in AI response");
            }

            // Extract summary
            if (!dataElement.TryGetProperty("summary", out var summaryElement))
            {
                return ResultResponse<CleanedArticle>.Failure("Missing 'summary' field in AI response");
            }

            var summary = summaryElement.GetString();
            if (string.IsNullOrWhiteSpace(summary))
            {
                return ResultResponse<CleanedArticle>.Failure("Summary is empty in AI response");
            }

            // Extract and parse category
            if (!dataElement.TryGetProperty("category", out var categoryElement))
            {
                return ResultResponse<CleanedArticle>.Failure("Missing 'category' field in AI response");
            }

            var categoryStr = categoryElement.GetString();
            if (string.IsNullOrWhiteSpace(categoryStr) ||
                !Enum.TryParse<CategoryEnum>(categoryStr, ignoreCase: true, out var category))
            {
                return ResultResponse<CleanedArticle>.Failure($"Invalid category '{categoryStr}' in AI response");
            }

            // Extract relevanceScore
            if (!dataElement.TryGetProperty("relevanceScore", out var relevanceElement))
            {
                return ResultResponse<CleanedArticle>.Failure("Missing 'relevanceScore' field in AI response");
            }

            var relevanceScore = relevanceElement.GetInt32();
            if (relevanceScore < 0 || relevanceScore > 100)
            {
                return ResultResponse<CleanedArticle>.Failure($"relevanceScore {relevanceScore} out of range 0-100");
            }

            // Extract severity (optional, only for security)
            SeverityEnum? severity = null;
            if (dataElement.TryGetProperty("severity", out var severityElement)
                && severityElement.ValueKind != JsonValueKind.Null)
            {
                var severityStr = severityElement.GetString();
                if (!string.IsNullOrWhiteSpace(severityStr) &&
                    Enum.TryParse<SeverityEnum>(severityStr, ignoreCase: true, out var parsedSeverity))
                {
                    severity = parsedSeverity;
                }
            }

            // Validate severity rules
            if (category == CategoryEnum.AiSafetyAndSecurity && !severity.HasValue)
            {
                return ResultResponse<CleanedArticle>.Failure(
                    "severity is required for AiSafetyAndSecurity category");
            }

            if (category != CategoryEnum.AiSafetyAndSecurity && severity.HasValue)
            {
                severity = null; // Ignore severity for non-security categories
            }

            // Extract tags (optional, max 5)
            List<string>? tags = null;
            if (dataElement.TryGetProperty("tags", out var tagsElement)
                && tagsElement.ValueKind == JsonValueKind.Array)
            {
                tags = new List<string>();
                foreach (var tagEl in tagsElement.EnumerateArray())
                {
                    var tagStr = tagEl.GetString();
                    if (!string.IsNullOrWhiteSpace(tagStr) && tags.Count < 5)
                    {
                        tags.Add(tagStr.ToLowerInvariant());
                    }
                }
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

            // Resolve source from URL using known mappings
            var source = ResolveSource(url);

            var cleanedArticle = new CleanedArticle(
                Title: title,
                Summary: summary,
                Category: category,
                Url: url,
                RelevanceScore: relevanceScore,
                PublishedAt: publishedAt,
                Source: source,
                Author: author,
                Severity: severity,
                Tags: tags
            );

            return ResultResponse<CleanedArticle>.Success(cleanedArticle);
        }
        catch (Exception ex)
        {
            return ResultResponse<CleanedArticle>.Failure($"Failed to parse AI response: {ex.Message}");
        }
    }
}