namespace DevNews.Infrastructure.Services;

internal static class JsonResponseHelper
{
    public static string CleanJsonResponse(string jsonResponse)
    {
        var cleaned = jsonResponse.Trim();
        if (cleaned.StartsWith("```json")) cleaned = cleaned[7..];
        if (cleaned.StartsWith("```")) cleaned = cleaned[3..];
        if (cleaned.EndsWith("```")) cleaned = cleaned[..^3];
        return cleaned.Trim();
    }
}
