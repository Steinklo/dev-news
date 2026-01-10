using Anthropic;
using Anthropic.Models.Messages;
using DevNews.Application.Common.Services;
using DevNews.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevNews.Infrastructure.Services;

public class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    /// <summary>
    /// Anthropic API key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model to use (e.g., claude-sonnet-4-20250514, claude-haiku-4-20250514)
    /// </summary>
    public string Model { get; set; } = "claude-sonnet-4-20250514";

    /// <summary>
    /// Maximum tokens in the response
    /// </summary>
    public int MaxTokens { get; set; } = 4096;
}

public class AnthropicAiService : IAiService
{
    private readonly AnthropicClient _client;
    private readonly AnthropicOptions _options;
    private readonly ILogger<AnthropicAiService> _logger;

    public AnthropicAiService(
        IOptions<AnthropicOptions> options,
        ILogger<AnthropicAiService> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Anthropic API key is not configured");
        }

        _client = new AnthropicClient { APIKey = _options.ApiKey };
    }

    public async Task<ResultResponse<string>> GenerateAsync(string prompt, CancellationToken ct = default)
    {
        try
        {
            _logger.LogDebug(
                "Calling Anthropic API with model {Model}, prompt length: {Length}",
                _options.Model,
                prompt.Length);

            var parameters = new MessageCreateParams
            {
                Model = _options.Model,
                MaxTokens = _options.MaxTokens,
                Messages =
                [
                    new MessageParam
                    {
                        Role = Role.User,
                        Content = prompt
                    }
                ]
            };

            var message = await _client.Messages.Create(parameters, ct);

            // Extract text content from response
            var textContent = string.Join("", message.Content.Select(c => c.ToString()));

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
