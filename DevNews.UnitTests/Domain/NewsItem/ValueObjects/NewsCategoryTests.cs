using DevNews.Domain.NewsItem.Enums;
using DevNews.Domain.NewsItem.ValueObjects;

namespace DevNews.UnitTests.Domain.NewsItem.ValueObjects;

public class NewsCategoryTests
{
    [Theory]
    [InlineData(CategoryEnum.SecurityAndVulnerabilities)]
    [InlineData(CategoryEnum.ProgrammingLanguagesAndRuntimes)]
    [InlineData(CategoryEnum.FrameworksAndLibraries)]
    [InlineData(CategoryEnum.CloudAndInfrastructure)]
    [InlineData(CategoryEnum.DevOpsCiCdObservabilityTesting)]
    [InlineData(CategoryEnum.AiMlDeveloperTooling)]
    [InlineData(CategoryEnum.PerformanceAndArchitecturePatterns)]
    [InlineData(CategoryEnum.DeveloperToolsIdesProductivity)]
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
        var result = NewsCategory.Create(CategoryEnum.SecurityAndVulnerabilities);
        CategoryEnum value = result.Data!;

        Assert.Equal(CategoryEnum.SecurityAndVulnerabilities, value);
    }

    [Fact]
    public void ToString_ReturnsEnumName()
    {
        var result = NewsCategory.Create(CategoryEnum.CloudAndInfrastructure);

        Assert.Equal("CloudAndInfrastructure", result.Data!.ToString());
    }

    [Fact]
    public void Equals_SameCategory_ReturnsTrue()
    {
        var category1 = NewsCategory.Create(CategoryEnum.AiMlDeveloperTooling).Data!;
        var category2 = NewsCategory.Create(CategoryEnum.AiMlDeveloperTooling).Data!;

        Assert.True(category1.Equals(category2));
    }

    [Fact]
    public void Equals_DifferentCategory_ReturnsFalse()
    {
        var category1 = NewsCategory.Create(CategoryEnum.SecurityAndVulnerabilities).Data!;
        var category2 = NewsCategory.Create(CategoryEnum.CloudAndInfrastructure).Data!;

        Assert.False(category1.Equals(category2));
    }

    [Fact]
    public void GetHashCode_SameCategory_ReturnsSameHash()
    {
        var category1 = NewsCategory.Create(CategoryEnum.FrameworksAndLibraries).Data!;
        var category2 = NewsCategory.Create(CategoryEnum.FrameworksAndLibraries).Data!;

        Assert.Equal(category1.GetHashCode(), category2.GetHashCode());
    }
}
