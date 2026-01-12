using DevNews.Domain.Common;

namespace DevNews.Domain.NewsItem.ValueObjects;

/// <summary>
/// Value object representing a news title with validation
/// </summary>
public class NewsTitle : ValueObject
{
    public string Value { get; private set; }

    /// <summary>
    /// Maximum title length - aligned with CurationRules
    /// </summary>
    public const int MaxLength = 100;

    /// <summary>
    /// Minimum title length - aligned with CurationRules
    /// </summary>
    public const int MinLength = 20;

    private NewsTitle(string value)
    {
        Value = value;
    }

    public static ResultResponse<NewsTitle> Create(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return ResultResponse<NewsTitle>.Failure("Title cannot be empty");

        var trimmed = title.Trim();

        if (trimmed.Length < MinLength)
            return ResultResponse<NewsTitle>.Failure($"Title must be at least {MinLength} characters long");

        if (trimmed.Length > MaxLength)
            return ResultResponse<NewsTitle>.Failure($"Title cannot exceed {MaxLength} characters");

        return ResultResponse<NewsTitle>.Success(new NewsTitle(trimmed));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(NewsTitle title) => title.Value;
}
