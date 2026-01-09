using DevNews.Domain.Common;
using DevNews.Domain.Common.Models;
using DevNews.Domain.NewsItem.Enums;
using DevNews.Domain.NewsItem.ValueObjects;

namespace DevNews.Application.Common.Services;

public static class CurationRules
{
    public static int MinTitleLength => 20;
    public static int MaxTitleLength => 100;
    public static int MinSummaryLength => 50;
    public static int MaxSummaryLength => 300;

    public static IReadOnlySet<CategoryEnum> AllowedCategories { get; } = Enum.GetValues<CategoryEnum>().ToHashSet();
}

public interface ICurationService
{
    Task<ResultResponse<CleanedArticle>> CurateAsync(
        CrawledArticle article,
        CancellationToken ct = default);
}

public record CurationResult(
    NewsTitle? Title,
    NewsSummary? Summary,
    NewsCategory? Category);