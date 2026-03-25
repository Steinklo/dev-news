using DevNews.Domain.NewsItem.Enums;
using DevNews.Domain.NewsItem.ValueObjects;

namespace DevNews.UnitTests.Domain.NewsItem.ValueObjects;

public class NewsCategoryTests
{
    [Theory]
    [InlineData(CategoryEnum.AiModelsAndApis)]
    [InlineData(CategoryEnum.AiDeveloperTools)]
    [InlineData(CategoryEnum.AgentsAndFrameworks)]
    [InlineData(CategoryEnum.AiEngineering)]
    [InlineData(CategoryEnum.AiSafetyAndSecurity)]
    [InlineData(CategoryEnum.InfrastructureAndCloud)]
    [InlineData(CategoryEnum.OpenSourceAndCommunity)]
    public void Create_ValidCategory_ReturnsSuccess(CategoryEnum category)
    {
        var result = NewsCategory.Create(category);

        Assert.True(result.IsSuccess);
        Assert.Equal(category, result.Data!.Value);
    }

    [Fact]
    public void Create_InvalidCategory_ReturnsFailure()
    {
        var invalidCategory = (CategoryEnum)999;

        var result = NewsCategory.Create(invalidCategory);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid category value", result.ErrorMessage);
    }

    [Fact]
    public void ImplicitConversion_ToCategoryEnum_ReturnsValue()
    {
        var result = NewsCategory.Create(CategoryEnum.AiSafetyAndSecurity);
        CategoryEnum value = result.Data!;

        Assert.Equal(CategoryEnum.AiSafetyAndSecurity, value);
    }

    [Fact]
    public void ToString_ReturnsEnumName()
    {
        var result = NewsCategory.Create(CategoryEnum.InfrastructureAndCloud);

        Assert.Equal("InfrastructureAndCloud", result.Data!.ToString());
    }

    [Fact]
    public void Equals_SameCategory_ReturnsTrue()
    {
        var category1 = NewsCategory.Create(CategoryEnum.AiDeveloperTools).Data!;
        var category2 = NewsCategory.Create(CategoryEnum.AiDeveloperTools).Data!;

        Assert.True(category1.Equals(category2));
    }

    [Fact]
    public void Equals_DifferentCategory_ReturnsFalse()
    {
        var category1 = NewsCategory.Create(CategoryEnum.AiSafetyAndSecurity).Data!;
        var category2 = NewsCategory.Create(CategoryEnum.InfrastructureAndCloud).Data!;

        Assert.False(category1.Equals(category2));
    }

    [Fact]
    public void GetHashCode_SameCategory_ReturnsSameHash()
    {
        var category1 = NewsCategory.Create(CategoryEnum.AgentsAndFrameworks).Data!;
        var category2 = NewsCategory.Create(CategoryEnum.AgentsAndFrameworks).Data!;

        Assert.Equal(category1.GetHashCode(), category2.GetHashCode());
    }
}
