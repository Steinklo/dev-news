using DevNews.Domain.Common;
using DevNews.Domain.Common.Models;
using DevNews.Domain.NewsItem.Enums;

namespace DevNews.Application.Common.Services;

public static class CurationRules
{
    public static int MinTitleLength => 20;
    public static int MaxTitleLength => 100;

    /// <summary>
    /// Minimum summary length in characters (~80 words at 5 chars/word)
    /// Per CLAUDE.md: TL;DR must be 80-160 words, dense, no fluff
    /// </summary>
    public static int MinSummaryLength => 400;

    /// <summary>
    /// Maximum summary length in characters (~160 words at 5 chars/word)
    /// Per CLAUDE.md: TL;DR must be 80-160 words, dense, no fluff
    /// </summary>
    public static int MaxSummaryLength => 1000;

    public static IReadOnlySet<CategoryEnum> AllowedCategories { get; } = Enum.GetValues<CategoryEnum>().ToHashSet();
}

public interface ICurationService
{
    Task<ResultResponse<CleanedArticle>> CurateAsync(
        CrawledArticle article,
        CancellationToken ct = default);
}