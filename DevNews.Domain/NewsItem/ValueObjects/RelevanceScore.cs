using DevNews.Domain.Common;

namespace DevNews.Domain.NewsItem.ValueObjects;

/// <summary>
/// Value object representing relevance score (0-100).
/// Indicates how relevant this news item is for professional developers.
/// </summary>
public class RelevanceScore : ValueObject
{
    public int Value { get; private set; }

    private RelevanceScore(int value)
    {
        Value = value;
    }

    public static ResultResponse<RelevanceScore> Create(int score)
    {
        if (score < 0 || score > 100)
            return ResultResponse<RelevanceScore>.Failure("Relevance score must be between 0 and 100");

        return ResultResponse<RelevanceScore>.Success(new RelevanceScore(score));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator int(RelevanceScore score) => score.Value;
}
