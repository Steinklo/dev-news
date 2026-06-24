using DevNews.Domain.SocialPost.ValueObjects;

namespace DevNews.UnitTests.Domain.SocialPost.ValueObjects;

public class SocialPostTextTests
{
    [Fact]
    public void Create_WithinBounds_Succeeds()
    {
        var result = SocialPostText.Create(new string('a', 200));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Create_OverThreeHundred_Fails()
    {
        var result = SocialPostText.Create(new string('a', 301));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_TooShort_Fails()
    {
        var result = SocialPostText.Create("too short");
        Assert.False(result.IsSuccess);
    }
}
