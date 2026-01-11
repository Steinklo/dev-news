using DevNews.Domain.Common;
using FluentAssertions;

namespace DevNews.UnitTests.Domain.Common;

public class ResultResponseTests
{
    [Fact]
    public void Success_CreatesSuccessResult()
    {
        var data = "test data";

        var result = ResultResponse<string>.Success(data);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(data);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Failure_CreatesFailureResult()
    {
        var errorMessage = "Something went wrong";

        var result = ResultResponse<string>.Failure(errorMessage);

        result.IsSuccess.Should().BeFalse();
        result.Data.Should().BeNull();
        result.ErrorMessage.Should().Be(errorMessage);
    }

    [Fact]
    public void Failure_WithNullMessage_UsesUnknownError()
    {
        var result = ResultResponse<string>.Failure(null);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Unknown error");
    }

    [Fact]
    public void ToString_Success_ReturnsSuccessFormat()
    {
        var result = ResultResponse<string>.Success("test");

        result.ToString().Should().Be("Success: test");
    }

    [Fact]
    public void ToString_Failure_ReturnsFailureFormat()
    {
        var result = ResultResponse<string>.Failure("error");

        result.ToString().Should().Be("Failure: error");
    }

    [Fact]
    public void Success_WithComplexType_StoresData()
    {
        var data = new { Name = "Test", Value = 42 };

        var result = ResultResponse<object>.Success(data);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(data);
    }

    [Fact]
    public void Failure_ForValueType_ReturnsDefault()
    {
        var result = ResultResponse<int>.Failure("error");

        result.IsSuccess.Should().BeFalse();
        result.Data.Should().Be(default(int));
    }
}
