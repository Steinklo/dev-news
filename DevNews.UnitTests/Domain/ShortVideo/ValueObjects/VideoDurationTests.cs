using DevNews.Domain.ShortVideo.ValueObjects;

namespace DevNews.UnitTests.Domain.ShortVideo.ValueObjects;

public class VideoDurationTests
{
    [Fact]
    public void Create_ValidDuration_ReturnsSuccess()
    {
        var result = VideoDuration.Create(30);

        Assert.True(result.IsSuccess);
        Assert.Equal(30, result.Data!.Seconds);
    }

    [Fact]
    public void Create_MinDuration_ReturnsSuccess()
    {
        var result = VideoDuration.Create(VideoDuration.MinSeconds);

        Assert.True(result.IsSuccess);
        Assert.Equal(VideoDuration.MinSeconds, result.Data!.Seconds);
    }

    [Fact]
    public void Create_MaxDuration_ReturnsSuccess()
    {
        var result = VideoDuration.Create(VideoDuration.MaxSeconds);

        Assert.True(result.IsSuccess);
        Assert.Equal(VideoDuration.MaxSeconds, result.Data!.Seconds);
    }

    [Fact]
    public void Create_BelowMinDuration_ReturnsFailure()
    {
        var result = VideoDuration.Create(VideoDuration.MinSeconds - 1);

        Assert.False(result.IsSuccess);
        Assert.Contains($"{VideoDuration.MinSeconds}", result.ErrorMessage);
    }

    [Fact]
    public void Create_AboveMaxDuration_ReturnsFailure()
    {
        var result = VideoDuration.Create(VideoDuration.MaxSeconds + 1);

        Assert.False(result.IsSuccess);
        Assert.Contains($"{VideoDuration.MaxSeconds}", result.ErrorMessage);
    }

    [Fact]
    public void ImplicitConversion_ToInt_ReturnsSeconds()
    {
        var result = VideoDuration.Create(45);
        int value = result.Data!;

        Assert.Equal(45, value);
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        var result = VideoDuration.Create(30);

        Assert.Equal("30s", result.Data!.ToString());
    }
}
