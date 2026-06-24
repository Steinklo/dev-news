using DevNews.Infrastructure.Services;

namespace DevNews.UnitTests.Infrastructure.Services;

public class AiSocialPostServiceTests
{
    [Fact]
    public void TruncateToFit_ShortText_Unchanged()
    {
        var text = "Short post about a thing https://example.com/a";
        Assert.Equal(text, AiSocialPostService.TruncateToFit(text, 300));
    }

    [Fact]
    public void TruncateToFit_LongText_TrimsBody_KeepsUrl_UnderMax()
    {
        var url = "https://example.com/some/article";
        var text = new string('a', 400) + " " + url;

        var result = AiSocialPostService.TruncateToFit(text, 300);

        Assert.True(result.Length <= 300, $"length was {result.Length}");
        Assert.EndsWith(url, result);
        Assert.Contains("…", result);
    }
}
