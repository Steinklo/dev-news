using DevNews.Domain.Common;

namespace DevNews.Domain.NewsItem.ValueObjects;

/// <summary>
/// Value object representing a news summary (TLDR)
/// </summary>
public class NewsSummary : ValueObject
{
    public string Value { get; private set; }
    public const int MaxLength = 500;
    public const int MinLength = 10;

    private NewsSummary(string value)
    {
        Value = value;
    }

    public static ResultResponse<NewsSummary> Create(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            return ResultResponse<NewsSummary>.Failure("Summary cannot be empty");

        var trimmed = summary.Trim();

        if (trimmed.Length < MinLength)
            return ResultResponse<NewsSummary>.Failure($"Summary must be at least {MinLength} characters long");

        if (trimmed.Length > MaxLength)
            return ResultResponse<NewsSummary>.Failure($"Summary cannot exceed {MaxLength} characters");

        return ResultResponse<NewsSummary>.Success(new NewsSummary(trimmed));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(NewsSummary summary) => summary.Value;
}
