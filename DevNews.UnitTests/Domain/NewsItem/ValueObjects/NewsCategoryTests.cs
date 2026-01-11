using DevNews.Domain.NewsItem.Enums;
using DevNews.Domain.NewsItem.ValueObjects;
using FluentAssertions;

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

        result.IsSuccess.Should().BeTrue();
        result.Data!.Value.Should().Be(category);
    }

    [Fact]
    public void Create_InvalidCategory_ReturnsFailure()
    {
        var invalidCategory = (CategoryEnum)999;

        var result = NewsCategory.Create(invalidCategory);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Invalid category value");
    }

    [Fact]
    public void ImplicitConversion_ToCategoryEnum_ReturnsValue()
    {
        var result = NewsCategory.Create(CategoryEnum.SecurityAndVulnerabilities);
        CategoryEnum value = result.Data!;

        value.Should().Be(CategoryEnum.SecurityAndVulnerabilities);
    }

    [Fact]
    public void ToString_ReturnsEnumName()
    {
        var result = NewsCategory.Create(CategoryEnum.CloudAndInfrastructure);

        result.Data!.ToString().Should().Be("CloudAndInfrastructure");
    }

    [Fact]
    public void Equals_SameCategory_ReturnsTrue()
    {
        var category1 = NewsCategory.Create(CategoryEnum.AiMlDeveloperTooling).Data!;
        var category2 = NewsCategory.Create(CategoryEnum.AiMlDeveloperTooling).Data!;

        category1.Equals(category2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentCategory_ReturnsFalse()
    {
        var category1 = NewsCategory.Create(CategoryEnum.SecurityAndVulnerabilities).Data!;
        var category2 = NewsCategory.Create(CategoryEnum.CloudAndInfrastructure).Data!;

        category1.Equals(category2).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_SameCategory_ReturnsSameHash()
    {
        var category1 = NewsCategory.Create(CategoryEnum.FrameworksAndLibraries).Data!;
        var category2 = NewsCategory.Create(CategoryEnum.FrameworksAndLibraries).Data!;

        category1.GetHashCode().Should().Be(category2.GetHashCode());
    }
}
