using DevNews.Domain.Common;

namespace DevNews.UnitTests.Domain.Common;

public class ResultResponseTests
{
    [Fact]
    public void Success_CreatesSuccessResult()
    {
        var data = "test data";

        var result = ResultResponse<string>.Success(data);

        Assert.True(result.IsSuccess);
        Assert.Equal(data, result.Data);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Failure_CreatesFailureResult()
    {
        var errorMessage = "Something went wrong";

        var result = ResultResponse<string>.Failure(errorMessage);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(errorMessage, result.ErrorMessage);
    }

    [Fact]
    public void Failure_WithNullMessage_UsesUnknownError()
    {
        var result = ResultResponse<string>.Failure(null);

        Assert.False(result.IsSuccess);
        Assert.Equal("Unknown error", result.ErrorMessage);
    }

    [Fact]
    public void ToString_Success_ReturnsSuccessFormat()
    {
        var result = ResultResponse<string>.Success("test");

        Assert.Equal("Success: test", result.ToString());
    }

    [Fact]
    public void ToString_Failure_ReturnsFailureFormat()
    {
        var result = ResultResponse<string>.Failure("error");

        Assert.Equal("Failure: error", result.ToString());
    }

    [Fact]
    public void Success_WithComplexType_StoresData()
    {
        var data = new { Name = "Test", Value = 42 };

        var result = ResultResponse<object>.Success(data);

        Assert.True(result.IsSuccess);
        Assert.Equal(data, result.Data);
    }

    [Fact]
    public void Failure_ForValueType_ReturnsDefault()
    {
        var result = ResultResponse<int>.Failure("error");

        Assert.False(result.IsSuccess);
        Assert.Equal(default(int), result.Data);
    }
}
