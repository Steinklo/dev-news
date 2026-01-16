using DevNews.Application.Common.Services;
using DevNews.Application.Common.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace DevNews.Functions.NightlyCrawl;

public class Orchestrator
{
    private static TaskOptions CreateRetryOptions()
    {
        return TaskOptions.FromRetryPolicy(new RetryPolicy(
            maxNumberOfAttempts: 3,
            firstRetryInterval: TimeSpan.FromSeconds(5),
            backoffCoefficient: 2.0));
    }

    [Function(nameof(NightlyCrawlOrchestrator))]
    public async Task<NightlyCrawlResult> NightlyCrawlOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger<Orchestrator>();
        var startTime = context.CurrentUtcDateTime;

        var discovered = 0;
        var curated = 0;
        var filteredLowRelevance = 0;
        var duplicates = 0;
        var persisted = 0;
        var failed = 0;

        logger.LogInformation("Starting nightly crawl orchestration");

        // Step 1: Discover articles
        List<CrawledArticle> crawledArticles;
        try
        {
            crawledArticles = await context.CallActivityAsync<List<CrawledArticle>>(
                nameof(Activities.DiscoverArticlesActivity),
                null,
                CreateRetryOptions());

            discovered = crawledArticles.Count;
            logger.LogInformation("Discovered {Count} articles", discovered);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Discovery failed");
            return new NightlyCrawlResult(0, 0, 0, 0, 0, 1, TimeSpan.Zero);
        }

        if (crawledArticles.Count == 0)
        {
            logger.LogInformation("No articles discovered, ending orchestration");
            return new NightlyCrawlResult(0, 0, 0, 0, 0, 0, context.CurrentUtcDateTime - startTime);
        }

        // Step 2: Curate articles sequentially to respect rate limits (50 req/min)
        var curationResults = new List<CleanedArticle?>();
        foreach (var article in crawledArticles)
        {
            var result = await context.CallActivityAsync<CleanedArticle?>(
                nameof(Activities.CurateArticleActivity),
                article,
                CreateRetryOptions());
            curationResults.Add(result);

            // Rate limit: 50 requests/min = 1.2s between calls, use 2s for safety
            await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(2), CancellationToken.None);
        }

        // Filter out failed curations
        var cleanedArticles = curationResults
            .Where(r => r != null)
            .Cast<CleanedArticle>()
            .ToList();

        curated = cleanedArticles.Count;
        failed += discovered - curated;
        logger.LogInformation("Curated {Count} articles, {Failed} failed", curated, discovered - curated);

        // Step 2.5: Filter out low-relevance articles (saves deduplication API costs)
        var relevantArticles = cleanedArticles
            .Where(a => a.RelevanceScore >= CrawlThresholds.MinRelevanceScore)
            .ToList();

        filteredLowRelevance = curated - relevantArticles.Count;
        if (filteredLowRelevance > 0)
        {
            logger.LogInformation(
                "Filtered {Count} articles with relevance < {Threshold}",
                filteredLowRelevance,
                CrawlThresholds.MinRelevanceScore);
        }

        // Step 3: Check duplications sequentially to respect rate limits (50 req/min)
        var duplicationResults = new List<(CleanedArticle Article, bool IsDuplicate)>();
        foreach (var article in relevantArticles)
        {
            var isDuplicate = await context.CallActivityAsync<bool>(
                nameof(Activities.CheckDuplicationActivity),
                article,
                CreateRetryOptions());
            duplicationResults.Add((article, isDuplicate));

            // Rate limit: 50 requests/min = 1.2s between calls, use 2s for safety
            await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(2), CancellationToken.None);
        }

        // Filter out duplicates
        var uniqueArticles = duplicationResults
            .Where(r => !r.IsDuplicate)
            .Select(r => r.Article)
            .ToList();

        duplicates = relevantArticles.Count - uniqueArticles.Count;
        logger.LogInformation("Found {Duplicates} duplicates, {Unique} unique articles", duplicates,
            uniqueArticles.Count);

        // Step 4: Fan-out - Persist unique articles in parallel
        var persistTasks = uniqueArticles.Select(article =>
            context.CallActivityAsync<Guid?>(
                nameof(Activities.PersistNewsItemActivity),
                article,
                CreateRetryOptions()));

        var persistResults = await Task.WhenAll(persistTasks);

        persisted = persistResults.Count(r => r.HasValue);
        failed += uniqueArticles.Count - persisted;

        var duration = context.CurrentUtcDateTime - startTime;

        logger.LogInformation(
            "Nightly crawl completed. Discovered: {Discovered}, Curated: {Curated}, FilteredLowRelevance: {FilteredLowRelevance}, Duplicates: {Duplicates}, Persisted: {Persisted}, Failed: {Failed}, Duration: {Duration}",
            discovered, curated, filteredLowRelevance, duplicates, persisted, failed, duration);

        return new NightlyCrawlResult(discovered, curated, filteredLowRelevance, duplicates, persisted, failed, duration);
    }
}