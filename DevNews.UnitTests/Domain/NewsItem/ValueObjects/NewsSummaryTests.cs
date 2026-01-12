using DevNews.Domain.NewsItem.ValueObjects;

namespace DevNews.UnitTests.Domain.NewsItem.ValueObjects;

public class NewsSummaryTests
{
    [Fact]
    public void Create_ValidSummary_ReturnsSuccess()
    {
        // 80+ words (~400 chars) per CLAUDE.md spec
        var validSummary = "This comprehensive security advisory details a critical remote code execution vulnerability " +
                           "discovered in the widely-used OpenSSL cryptographic library. The flaw, identified as CVE-2026-1234, " +
                           "affects versions 3.0 through 3.2.1 and allows unauthenticated attackers to execute arbitrary code " +
                           "on vulnerable systems. Organizations running affected versions should immediately upgrade to the " +
                           "patched release 3.2.2. The vulnerability was responsibly disclosed by security researchers and " +
                           "has been assigned a CVSS score of 9.8 indicating critical severity.";

        var result = NewsSummary.Create(validSummary);

        Assert.True(result.IsSuccess);
        Assert.Equal(validSummary, result.Data!.Value);
    }

    [Fact]
    public void Create_TrimsWhitespace()
    {
        var summaryContent = "This comprehensive security advisory details a critical remote code execution vulnerability " +
                             "discovered in the widely-used OpenSSL cryptographic library. The flaw affects versions 3.0 through 3.2.1 " +
                             "and allows unauthenticated attackers to execute arbitrary code on vulnerable systems. Organizations " +
                             "running affected versions should immediately upgrade to the patched release version 3.2.2 now available.";
        var summaryWithSpaces = $"  {summaryContent}  ";

        var result = NewsSummary.Create(summaryWithSpaces);

        Assert.True(result.IsSuccess);
        Assert.Equal(summaryContent, result.Data!.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyOrWhitespace_ReturnsFailure(string? summary)
    {
        var result = NewsSummary.Create(summary!);

        Assert.False(result.IsSuccess);
        Assert.Equal("Summary cannot be empty", result.ErrorMessage);
    }

    [Fact]
    public void Create_BelowMinLength_ReturnsFailure()
    {
        var shortSummary = new string('a', NewsSummary.MinLength - 1);

        var result = NewsSummary.Create(shortSummary);

        Assert.False(result.IsSuccess);
        Assert.Contains($"{NewsSummary.MinLength}", result.ErrorMessage);
    }

    [Fact]
    public void Create_ExceedsMaxLength_ReturnsFailure()
    {
        var longSummary = new string('a', NewsSummary.MaxLength + 1);

        var result = NewsSummary.Create(longSummary);

        Assert.False(result.IsSuccess);
        Assert.Contains($"{NewsSummary.MaxLength}", result.ErrorMessage);
    }

    [Fact]
    public void Create_ExactlyMinLength_ReturnsSuccess()
    {
        var minSummary = new string('a', NewsSummary.MinLength);

        var result = NewsSummary.Create(minSummary);

        Assert.True(result.IsSuccess);
        Assert.Equal(NewsSummary.MinLength, result.Data!.Value.Length);
    }

    [Fact]
    public void Create_ExactlyMaxLength_ReturnsSuccess()
    {
        var maxSummary = new string('a', NewsSummary.MaxLength);

        var result = NewsSummary.Create(maxSummary);

        Assert.True(result.IsSuccess);
        Assert.Equal(NewsSummary.MaxLength, result.Data!.Value.Length);
    }

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        var summaryText = "This comprehensive security advisory details a critical remote code execution vulnerability " +
                          "discovered in the widely-used OpenSSL cryptographic library. The flaw affects versions 3.0 through 3.2.1 " +
                          "and allows unauthenticated attackers to execute arbitrary code on vulnerable systems. Organizations " +
                          "running affected versions should immediately upgrade to the patched release version 3.2.2 now available.";
        var result = NewsSummary.Create(summaryText);
        string value = result.Data!;

        Assert.Equal(summaryText, value);
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        var summaryText = "This comprehensive security advisory details a critical remote code execution vulnerability " +
                          "discovered in the widely-used OpenSSL cryptographic library. The flaw affects versions 3.0 through 3.2.1 " +
                          "and allows unauthenticated attackers to execute arbitrary code on vulnerable systems. Organizations " +
                          "running affected versions should immediately upgrade to the patched release version 3.2.2 now available.";
        var result = NewsSummary.Create(summaryText);

        Assert.Equal(summaryText, result.Data!.ToString());
    }

    [Fact]
    public void Equals_SameSummary_ReturnsTrue()
    {
        var text = "This comprehensive security advisory details a critical remote code execution vulnerability " +
                   "discovered in the widely-used OpenSSL cryptographic library. The flaw affects versions 3.0 through 3.2.1 " +
                   "and allows unauthenticated attackers to execute arbitrary code on vulnerable systems. Organizations " +
                   "running affected versions should immediately upgrade to the patched release version 3.2.2 now available.";
        var summary1 = NewsSummary.Create(text).Data!;
        var summary2 = NewsSummary.Create(text).Data!;

        Assert.True(summary1.Equals(summary2));
    }

    [Fact]
    public void Equals_DifferentSummary_ReturnsFalse()
    {
        var summary1 = NewsSummary.Create(
            "This comprehensive security advisory details a critical remote code execution vulnerability " +
            "discovered in the widely-used OpenSSL cryptographic library. The flaw affects versions 3.0 through 3.2.1 " +
            "and allows unauthenticated attackers to execute arbitrary code on vulnerable systems. Organizations " +
            "running affected versions should immediately upgrade to the patched release version 3.2.2 now available.").Data!;
        var summary2 = NewsSummary.Create(
            "A new performance optimization in the Go runtime significantly reduces garbage collection pause times " +
            "for applications with large heaps. The improvement, landing in Go 1.24, achieves up to 40% reduction in " +
            "p99 latency for memory-intensive workloads. Developers should update their CI pipelines to test against " +
            "the latest release candidate before the stable version ships next month as scheduled.").Data!;

        Assert.False(summary1.Equals(summary2));
    }

    [Fact]
    public void GetHashCode_SameSummary_ReturnsSameHash()
    {
        var text = "This comprehensive security advisory details a critical remote code execution vulnerability " +
                   "discovered in the widely-used OpenSSL cryptographic library. The flaw affects versions 3.0 through 3.2.1 " +
                   "and allows unauthenticated attackers to execute arbitrary code on vulnerable systems. Organizations " +
                   "running affected versions should immediately upgrade to the patched release version 3.2.2 now available.";
        var summary1 = NewsSummary.Create(text).Data!;
        var summary2 = NewsSummary.Create(text).Data!;

        Assert.Equal(summary1.GetHashCode(), summary2.GetHashCode());
    }
}
