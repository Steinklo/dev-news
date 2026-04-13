using DevNews.Domain.Common;

namespace DevNews.Domain.ShortVideo.ValueObjects;

/// <summary>
/// Value object representing a video narration script.
/// Must be plain text, 200-1000 characters (~40-200 words).
/// </summary>
public class VideoScript : ValueObject
{
    public string Value { get; private set; }

    public const int MinLength = 200;
    public const int MaxLength = 1000;

    private VideoScript(string value)
    {
        Value = value;
    }

    public static ResultResponse<VideoScript> Create(string script)
    {
        if (string.IsNullOrWhiteSpace(script))
            return ResultResponse<VideoScript>.Failure("Script cannot be empty");

        var trimmed = script.Trim();

        if (trimmed.Length < MinLength)
            return ResultResponse<VideoScript>.Failure($"Script must be at least {MinLength} characters long");

        if (trimmed.Length > MaxLength)
            return ResultResponse<VideoScript>.Failure($"Script cannot exceed {MaxLength} characters");

        return ResultResponse<VideoScript>.Success(new VideoScript(trimmed));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(VideoScript script) => script.Value;

    internal static VideoScript Reconstitute(string value) => new(value);
}
