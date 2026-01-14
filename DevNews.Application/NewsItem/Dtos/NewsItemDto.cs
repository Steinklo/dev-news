using System.Text.Json.Serialization;

namespace DevNews.Application.NewsItem.Dtos;

/// <summary>
/// DTO for news item data returned from queries.
/// </summary>
public record NewsItemDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("relevance_score")] int RelevanceScore,
    [property: JsonPropertyName("source")] string? Source,
    [property: JsonPropertyName("author")] string? Author,
    [property: JsonPropertyName("severity")] string? Severity,
    [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags,
    [property: JsonPropertyName("published_at")] DateTimeOffset? PublishedAt,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset? UpdatedAt)
{
    public static NewsItemDto FromDomain(Domain.NewsItem.NewsItem item)
    {
        return new NewsItemDto(
            Id: item.Id.ToString(),
            Title: item.Title.Value,
            Summary: item.Summary.Value,
            Url: item.Url.Value,
            Category: item.Category.Value.ToString(),
            RelevanceScore: item.RelevanceScore.Value,
            Source: item.Source,
            Author: item.Author,
            Severity: item.Severity?.ToString(),
            Tags: item.Tags.ToList(),
            PublishedAt: item.PublishedAt,
            CreatedAt: item.CreatedAt,
            UpdatedAt: item.UpdatedAt);
    }
}
