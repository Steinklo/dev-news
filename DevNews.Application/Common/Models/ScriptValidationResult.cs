namespace DevNews.Application.Common.Models;

/// <summary>
/// Result of AI-powered script validation.
/// </summary>
public record ScriptValidationResult(
    bool IsValid,
    string? Reason,
    int QualityScore);
