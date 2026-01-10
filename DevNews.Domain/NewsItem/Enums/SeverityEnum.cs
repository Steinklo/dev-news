namespace DevNews.Domain.NewsItem.Enums;

/// <summary>
/// Severity levels for security-related news items.
/// Only applicable when Category is SecurityAndVulnerabilities.
/// </summary>
public enum SeverityEnum
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}
