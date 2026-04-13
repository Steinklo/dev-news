using DevNews.Infrastructure.Services;

namespace DevNews.UnitTests.Infrastructure.Services;

public class JsonResponseHelperTests
{
    [Fact]
    public void CleanJsonResponse_AlreadyCleanJson_ReturnsUnchanged()
    {
        var json = """{"key": "value"}""";

        var result = JsonResponseHelper.CleanJsonResponse(json);

        Assert.Equal(json, result);
    }

    [Fact]
    public void CleanJsonResponse_WrappedInJsonCodeFence_StripsMarkers()
    {
        var json = """
            ```json
            {"key": "value"}
            ```
            """;

        var result = JsonResponseHelper.CleanJsonResponse(json);

        Assert.Equal("""{"key": "value"}""", result);
    }

    [Fact]
    public void CleanJsonResponse_WrappedInCodeFenceWithoutLanguage_StripsMarkers()
    {
        var json = """
            ```
            {"key": "value"}
            ```
            """;

        var result = JsonResponseHelper.CleanJsonResponse(json);

        Assert.Equal("""{"key": "value"}""", result);
    }

    [Fact]
    public void CleanJsonResponse_WithWhitespace_Trims()
    {
        var json = "  \n  {\"key\": \"value\"}  \n  ";

        var result = JsonResponseHelper.CleanJsonResponse(json);

        Assert.Equal("""{"key": "value"}""", result);
    }
}
