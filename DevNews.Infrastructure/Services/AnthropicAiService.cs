using Anthropic;
using Anthropic.Models.Messages;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DevNews.Infrastructure.Services;

public static class AnthropicOptions
{
    /// <summary>
    /// Model to use (e.g., claude-haiku-4-5, claude-sonnet-4-6)
    /// </summary>
    public static string Model => "claude-haiku-4-5";

    /// <summary>
    /// Maximum tokens in the response
    /// </summary>
    public static int MaxTokens => 4096;
}

public class AnthropicAiService : IAiService
{
    private readonly AnthropicClient _client;
    private readonly ILogger<AnthropicAiService> _logger;

    public AnthropicAiService(
        IConfiguration configuration,
        ILogger<AnthropicAiService> logger)
    {
        _logger = logger;
        var apiKey = configuration["AnthropicApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Anthropic API key is not configured");
        }

        _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<ResultResponse<string>> GenerateAsync(string prompt, string? modelOverride = null, CancellationToken ct = default)
    {
        try
        {
            var model = modelOverride ?? AnthropicOptions.Model;

            _logger.LogDebug(
                "Calling Anthropic API with model {Model}, prompt length: {Length}",
                model,
                prompt.Length);

            var parameters = new MessageCreateParams
            {
                Model = model,
                MaxTokens = AnthropicOptions.MaxTokens,
                System = "You are a JSON-only API. Output valid JSON without any preamble, explanation, or markdown formatting.",
                Messages =
                [
                    new MessageParam
                    {
                        Role = Role.User,
                        Content = prompt
                    },
                    // Prefill assistant response to force JSON output
                    new MessageParam
                    {
                        Role = Role.Assistant,
                        Content = "{"
                    }
                ]
            };

            var message = await _client.Messages.Create(parameters, ct);

            // Extract text content from response and prepend the prefilled "{"
            var rawContent = "";
            foreach (var block in message.Content)
            {
                if (block.Value is Anthropic.Models.Messages.TextBlock textBlock)
                {
                    rawContent += textBlock.Text ?? "";
                }
            }

            var textContent = "{" + rawContent;

            if (string.IsNullOrWhiteSpace(textContent))
            {
                _logger.LogWarning("Anthropic returned empty response");
                return ResultResponse<string>.Failure("AI returned empty response");
            }

            _logger.LogDebug(
                "Anthropic response received, length: {Length}, stop reason: {StopReason}",
                textContent.Length,
                message.StopReason);

            return ResultResponse<string>.Success(textContent);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Anthropic API HTTP error: {Message}", ex.Message);
            return ResultResponse<string>.Failure($"AI service error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling Anthropic API");
            return ResultResponse<string>.Failure($"AI service error: {ex.Message}");
        }
    }
}