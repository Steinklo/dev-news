using DevNews.Domain.Common;

namespace DevNews.Domain.SocialPost.ValueObjects;

public class SocialPostText : ValueObject
{
    public string Value { get; private set; }

    public const int MinLength = 100;
    public const int MaxLength = 2800;

    private SocialPostText(string value)
    {
        Value = value;
    }

    public static ResultResponse<SocialPostText> Create(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return ResultResponse<SocialPostText>.Failure("Social post text cannot be empty");

        var trimmed = content.Trim();

        if (trimmed.Length < MinLength)
            return ResultResponse<SocialPostText>.Failure($"Social post text must be at least {MinLength} characters long");

        if (trimmed.Length > MaxLength)
            return ResultResponse<SocialPostText>.Failure($"Social post text cannot exceed {MaxLength} characters");

        return ResultResponse<SocialPostText>.Success(new SocialPostText(trimmed));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(SocialPostText content) => content.Value;

    internal static SocialPostText Reconstitute(string value) => new(value);
}
