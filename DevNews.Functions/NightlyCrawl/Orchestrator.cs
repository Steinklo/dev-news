using DevNews.Application.Common.Services;
using DevNews.Domain.Common.Models;
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
            return new NightlyCrawlResult(0, 0, 0, 0, 1, TimeSpan.Zero);
        }

        if (crawledArticles.Count == 0)
        {
            logger.LogInformation("No articles discovered, ending orchestration");
            return new NightlyCrawlResult(0, 0, 0, 0, 0, context.CurrentUtcDateTime - startTime);
        }

        // Step 2: Fan-out - Curate all articles in parallel
        var curationTasks = crawledArticles.Select(article =>
            context.CallActivityAsync<CleanedArticle?>(
                nameof(Activities.CurateArticleActivity),
                article,
                CreateRetryOptions()));

        var curationResults = await Task.WhenAll(curationTasks);

        // Filter out failed curations
        var cleanedArticles = curationResults
            .Where(r => r != null)
            .Cast<CleanedArticle>()
            .ToList();

        curated = cleanedArticles.Count;
        failed += discovered - curated;
        logger.LogInformation("Curated {Count} articles, {Failed} failed", curated, discovered - curated);

        // Step 3: Fan-out - Check duplications in parallel
        var duplicationTasks = cleanedArticles.Select(async article =>
        {
            var isDuplicate = await context.CallActivityAsync<bool>(
                nameof(Activities.CheckDuplicationActivity),
                article,
                CreateRetryOptions());
            return (Article: article, IsDuplicate: isDuplicate);
        });

        var duplicationResults = await Task.WhenAll(duplicationTasks);

        // Filter out duplicates
        var uniqueArticles = duplicationResults
            .Where(r => !r.IsDuplicate)
            .Select(r => r.Article)
            .ToList();

        duplicates = curated - uniqueArticles.Count;
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
            "Nightly crawl completed. Discovered: {Discovered}, Curated: {Curated}, Duplicates: {Duplicates}, Persisted: {Persisted}, Failed: {Failed}, Duration: {Duration}",
            discovered, curated, duplicates, persisted, failed, duration);

        return new NightlyCrawlResult(discovered, curated, duplicates, persisted, failed, duration);
    }
}