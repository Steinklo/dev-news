using DevNews.Domain.Common;

namespace DevNews.Domain.VideoContent.ValueObjects;

/// <summary>
/// Value object representing a video script.
/// Enforces word count limits based on speaking rate (150 words/min).
/// </summary>
public class VideoScript : ValueObject
{
    public string Value { get; private set; }

    /// <summary>
    /// Maximum words for a 60-second video at 150 words/min speaking rate.
    /// </summary>
    public const int MaxWords = 160;

    /// <summary>
    /// Minimum words to ensure meaningful content.
    /// </summary>
    public const int MinWords = 20;

    private VideoScript(string value)
    {
        Value = value;
    }

    public static ResultResponse<VideoScript> Create(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return ResultResponse<VideoScript>.Failure("Video script cannot be empty");

        var trimmed = content.Trim();
        var wordCount = CountWords(trimmed);

        if (wordCount < MinWords)
            return ResultResponse<VideoScript>.Failure($"Script must contain at least {MinWords} words (currently: {wordCount})");

        if (wordCount > MaxWords)
            return ResultResponse<VideoScript>.Failure($"Script cannot exceed {MaxWords} words (currently: {wordCount})");

        return ResultResponse<VideoScript>.Success(new VideoScript(trimmed));
    }

    private static int CountWords(string text)
    {
        return text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(VideoScript script) => script.Value;

    internal static VideoScript Reconstitute(string value) => new(value);
}
