using DevNews.Domain.Common;

namespace DevNews.Domain.VideoContent.ValueObjects;

/// <summary>
/// Value object representing video duration in seconds.
/// Enforces platform constraints for short-form video.
/// </summary>
public class VideoDuration : ValueObject
{
    public int Seconds { get; private set; }

    /// <summary>
    /// Minimum duration for short-form video (15 seconds).
    /// </summary>
    public const int MinSeconds = 15;

    /// <summary>
    /// Maximum duration for short-form video (60 seconds).
    /// </summary>
    public const int MaxSeconds = 60;

    private VideoDuration(int seconds)
    {
        Seconds = seconds;
    }

    public static ResultResponse<VideoDuration> Create(int seconds)
    {
        if (seconds < MinSeconds)
            return ResultResponse<VideoDuration>.Failure($"Video duration must be at least {MinSeconds} seconds");

        if (seconds > MaxSeconds)
            return ResultResponse<VideoDuration>.Failure($"Video duration cannot exceed {MaxSeconds} seconds");

        return ResultResponse<VideoDuration>.Success(new VideoDuration(seconds));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Seconds;
    }

    public override string ToString() => $"{Seconds}s";

    public static implicit operator int(VideoDuration duration) => duration.Seconds;

    internal static VideoDuration Reconstitute(int seconds) => new(seconds);
}
