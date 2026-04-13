using DevNews.Domain.NewsItem.Enums;

namespace DevNews.UnitTests.Application;

/// <summary>
/// Shared valid test data for Application layer handler tests.
/// </summary>
internal static class TestData
{
    /// <summary>
    /// Valid summary meeting the 400+ char minimum requirement.
    /// </summary>
    public const string ValidSummary =
        "This comprehensive security advisory details a critical remote code execution vulnerability " +
        "discovered in the widely-used OpenSSL cryptographic library. The flaw, identified as CVE-2026-1234, " +
        "affects versions 3.0 through 3.2.1 and allows unauthenticated attackers to execute arbitrary code " +
        "on vulnerable systems. Organizations running affected versions should immediately upgrade to the " +
        "patched release 3.2.2. The vulnerability was responsibly disclosed by security researchers and " +
        "has been assigned a CVSS score of 9.8 indicating critical severity.";

    public const string ValidTitle = "Critical Security Vulnerability Found in Popular Library";
    public const string ValidUrl = "https://example.com/article";

    /// <summary>
    /// Valid video script meeting the 200+ char minimum requirement.
    /// </summary>
    public const string ValidScript =
        "Breaking news in the AI developer world today. A critical security vulnerability has been discovered " +
        "in a widely-used open-source library. The flaw allows remote code execution and affects thousands " +
        "of production systems worldwide. Security researchers recommend immediate patching.";

    public static DevNews.Domain.NewsItem.NewsItem CreateValidNewsItem(
        CategoryEnum category = CategoryEnum.AiModelsAndApis,
        int relevanceScore = 85)
    {
        return DevNews.Domain.NewsItem.NewsItem.Create(
            title: ValidTitle,
            summary: ValidSummary,
            url: ValidUrl,
            category: category,
            relevanceScore: relevanceScore).Data!;
    }
}
