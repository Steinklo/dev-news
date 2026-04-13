using DevNews.Domain.Common;

namespace DevNews.Domain.ShortVideo.ValueObjects;

/// <summary>
/// Value object representing video duration in seconds (15-60s for short-form content).
/// </summary>
public class VideoDuration : ValueObject
{
    public int Seconds { get; private set; }

    public const int MinSeconds = 15;
    public const int MaxSeconds = 60;

    private VideoDuration(int seconds)
    {
        Seconds = seconds;
    }

    public static ResultResponse<VideoDuration> Create(int seconds)
    {
        if (seconds < MinSeconds)
            return ResultResponse<VideoDuration>.Failure($"Video must be at least {MinSeconds} seconds long");

        if (seconds > MaxSeconds)
            return ResultResponse<VideoDuration>.Failure($"Video cannot exceed {MaxSeconds} seconds");

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
