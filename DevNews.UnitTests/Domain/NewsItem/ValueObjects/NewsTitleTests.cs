using DevNews.Domain.NewsItem.ValueObjects;

namespace DevNews.UnitTests.Domain.NewsItem.ValueObjects;

public class NewsTitleTests
{
    [Fact]
    public void Create_ValidTitle_ReturnsSuccess()
    {
        var title = "Critical OpenSSL Vulnerability Discovered";

        var result = NewsTitle.Create(title);

        Assert.True(result.IsSuccess);
        Assert.Equal(title, result.Data!.Value);
    }

    [Fact]
    public void Create_TrimsWhitespace()
    {
        var result = NewsTitle.Create("  Security Advisory for Node.js  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("Security Advisory for Node.js", result.Data!.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyOrWhitespace_ReturnsFailure(string? title)
    {
        var result = NewsTitle.Create(title!);

        Assert.False(result.IsSuccess);
        Assert.Equal("Title cannot be empty", result.ErrorMessage);
    }

    [Fact]
    public void Create_ExceedsMaxLength_ReturnsFailure()
    {
        var longTitle = new string('a', NewsTitle.MaxLength + 1);

        var result = NewsTitle.Create(longTitle);

        Assert.False(result.IsSuccess);
        Assert.Contains($"{NewsTitle.MaxLength}", result.ErrorMessage);
    }

    [Fact]
    public void Create_ExactlyMaxLength_ReturnsSuccess()
    {
        var exactTitle = new string('a', NewsTitle.MaxLength);

        var result = NewsTitle.Create(exactTitle);

        Assert.True(result.IsSuccess);
        Assert.Equal(NewsTitle.MaxLength, result.Data!.Value.Length);
    }

    [Fact]
    public void Create_MinimumLength_ReturnsSuccess()
    {
        var minTitle = new string('a', NewsTitle.MinLength);

        var result = NewsTitle.Create(minTitle);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Create_BelowMinLength_ReturnsFailure()
    {
        var shortTitle = new string('a', NewsTitle.MinLength - 1);

        var result = NewsTitle.Create(shortTitle);

        Assert.False(result.IsSuccess);
        Assert.Contains($"{NewsTitle.MinLength}", result.ErrorMessage);
    }

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        var title = "Kubernetes 1.30 Release Notes";
        var result = NewsTitle.Create(title);
        string value = result.Data!;

        Assert.Equal(title, value);
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        var title = "Docker Security Best Practices";
        var result = NewsTitle.Create(title);

        Assert.Equal(title, result.Data!.ToString());
    }

    [Fact]
    public void Equals_SameTitle_ReturnsTrue()
    {
        var title = "React 19 Breaking Changes";
        var title1 = NewsTitle.Create(title).Data!;
        var title2 = NewsTitle.Create(title).Data!;

        Assert.True(title1.Equals(title2));
    }

    [Fact]
    public void Equals_DifferentTitle_ReturnsFalse()
    {
        var title1 = NewsTitle.Create("Go 1.24 Performance Updates").Data!;
        var title2 = NewsTitle.Create("Rust 2.0 Memory Safety Fix").Data!;

        Assert.False(title1.Equals(title2));
    }

    [Fact]
    public void GetHashCode_SameTitle_ReturnsSameHash()
    {
        var title = "AWS Lambda Cold Start Fixes";
        var title1 = NewsTitle.Create(title).Data!;
        var title2 = NewsTitle.Create(title).Data!;

        Assert.Equal(title1.GetHashCode(), title2.GetHashCode());
    }
}
