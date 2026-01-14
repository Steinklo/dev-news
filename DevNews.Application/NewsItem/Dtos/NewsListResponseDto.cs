using System.Text.Json.Serialization;

namespace DevNews.Application.NewsItem.Dtos;

/// <summary>
/// Response DTO for paginated news list by category.
/// </summary>
public record NewsListResponseDto(
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("year_month")] string YearMonth,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("items")] IReadOnlyList<NewsItemDto> Items);

/// <summary>
/// Response DTO for categories list.
/// </summary>
public record CategoriesResponseDto(
    [property: JsonPropertyName("categories")] IReadOnlyList<CategoryDto> Categories);

/// <summary>
/// DTO for a single category.
/// </summary>
public record CategoryDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name);
