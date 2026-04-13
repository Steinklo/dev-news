using DevNews.Domain.ShortVideo.ValueObjects;

namespace DevNews.UnitTests.Domain.ShortVideo.ValueObjects;

public class VideoScriptTests
{
    private static string ValidScript => new('a', VideoScript.MinLength);

    [Fact]
    public void Create_ValidScript_ReturnsSuccess()
    {
        var result = VideoScript.Create(ValidScript);

        Assert.True(result.IsSuccess);
        Assert.Equal(ValidScript, result.Data!.Value);
    }

    [Fact]
    public void Create_TrimsWhitespace()
    {
        var script = $"  {ValidScript}  ";
        var result = VideoScript.Create(script);

        Assert.True(result.IsSuccess);
        Assert.Equal(ValidScript, result.Data!.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyOrWhitespace_ReturnsFailure(string? script)
    {
        var result = VideoScript.Create(script!);

        Assert.False(result.IsSuccess);
        Assert.Equal("Script cannot be empty", result.ErrorMessage);
    }

    [Fact]
    public void Create_BelowMinLength_ReturnsFailure()
    {
        var shortScript = new string('a', VideoScript.MinLength - 1);

        var result = VideoScript.Create(shortScript);

        Assert.False(result.IsSuccess);
        Assert.Contains($"{VideoScript.MinLength}", result.ErrorMessage);
    }

    [Fact]
    public void Create_ExceedsMaxLength_ReturnsFailure()
    {
        var longScript = new string('a', VideoScript.MaxLength + 1);

        var result = VideoScript.Create(longScript);

        Assert.False(result.IsSuccess);
        Assert.Contains($"{VideoScript.MaxLength}", result.ErrorMessage);
    }

    [Fact]
    public void Create_ExactlyMinLength_ReturnsSuccess()
    {
        var script = new string('a', VideoScript.MinLength);

        var result = VideoScript.Create(script);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Create_ExactlyMaxLength_ReturnsSuccess()
    {
        var script = new string('a', VideoScript.MaxLength);

        var result = VideoScript.Create(script);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        var result = VideoScript.Create(ValidScript);
        string value = result.Data!;

        Assert.Equal(ValidScript, value);
    }

    [Fact]
    public void Equals_SameScript_ReturnsTrue()
    {
        var script1 = VideoScript.Create(ValidScript).Data!;
        var script2 = VideoScript.Create(ValidScript).Data!;

        Assert.True(script1.Equals(script2));
    }

    [Fact]
    public void Equals_DifferentScript_ReturnsFalse()
    {
        var script1 = VideoScript.Create(new string('a', VideoScript.MinLength)).Data!;
        var script2 = VideoScript.Create(new string('b', VideoScript.MinLength)).Data!;

        Assert.False(script1.Equals(script2));
    }
}
