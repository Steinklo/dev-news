using System.Net;
using System.Text;
using System.Text.Json;
using DevNews.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DevNews.UnitTests.Infrastructure.Services;

public class CreatomateVideoGenerationServiceTests
{
    private readonly FakeHttpHandler _httpHandler = new();
    private readonly CreatomateVideoGenerationService _sut;

    public CreatomateVideoGenerationServiceTests()
    {
        var httpClient = new HttpClient(_httpHandler);
        var configuration = Substitute.For<IConfiguration>();
        configuration["CreatomateApiKey"].Returns("test-api-key");
        configuration["VideoGeneration:TtsVoiceName"].Returns("en-US-AndrewMultilingualNeural");

        _sut = new CreatomateVideoGenerationService(
            httpClient,
            configuration,
            NullLogger<CreatomateVideoGenerationService>.Instance);
    }

    [Fact]
    public void BuildVideoSource_ContainsExpectedElements_NoDallE()
    {
        var source = _sut.BuildVideoSource("Test script for video", "Test Title");
        var json = JsonSerializer.Serialize(source);

        Assert.Contains("Test Title", json);              // title text
        Assert.Contains("Test script for video", json);   // voiceover source
        Assert.Contains("microsoft", json);               // Azure TTS via Creatomate
        Assert.Contains("en-US-AndrewMultilingualNeural", json);
        Assert.Contains("wipe", json);                    // progress bar
        Assert.Contains("transcript_source", json);       // synced captions
        Assert.DoesNotContain("dall-e", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("openai", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildVideoSource_CaptionsReferenceTheNamedVoiceover()
    {
        var source = _sut.BuildVideoSource("Script", "GPT-5 Released Today");
        var json = JsonSerializer.Serialize(source);
        using var doc = JsonDocument.Parse(json);

        Assert.Contains("GPT-5 Released Today", json);    // title text still present
        Assert.DoesNotContain("cinematic", json);         // old DALL-E prompt gone

        var elements = doc.RootElement.GetProperty("elements").EnumerateArray().ToList();
        var audioName = elements
            .First(e => e.GetProperty("type").GetString() == "audio")
            .GetProperty("name").GetString();
        var captionSource = elements
            .First(e => e.TryGetProperty("transcript_source", out _))
            .GetProperty("transcript_source").GetString();

        // The caption is wired to the voiceover by name — this coupling must hold.
        Assert.Equal(audioName, captionSource);
    }

    [Fact]
    public void BuildVideoSource_VoiceoverMatchesScript()
    {
        var script = "This is the narration script text.";
        var source = _sut.BuildVideoSource(script, "Title");
        var json = JsonSerializer.Serialize(source);
        using var doc = JsonDocument.Parse(json);

        var elements = doc.RootElement.GetProperty("elements");
        var audioElement = elements.EnumerateArray()
            .First(e => e.GetProperty("type").GetString() == "audio");

        Assert.Equal(script, audioElement.GetProperty("source").GetString());
    }

    [Fact]
    public void BuildVideoSource_OutputFormatIsMp4_9x16()
    {
        var source = _sut.BuildVideoSource("Script", "Title");
        var json = JsonSerializer.Serialize(source);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal("mp4", doc.RootElement.GetProperty("output_format").GetString());
        Assert.Equal(1080, doc.RootElement.GetProperty("width").GetInt32());
        Assert.Equal(1920, doc.RootElement.GetProperty("height").GetInt32());
    }

    [Fact]
    public async Task GenerateVideoAsync_SuccessfulRender_ReturnsGeneratedVideo()
    {
        var renderId = "render-123";
        _httpHandler.SetupJsonResponse(
            "https://api.creatomate.com/v1/renders",
            "[{\"id\": \"" + renderId + "\", \"status\": \"planned\"}]");

        _httpHandler.SetupJsonResponse(
            "https://api.creatomate.com/v1/renders/" + renderId,
            "{\"id\": \"" + renderId + "\", \"status\": \"succeeded\", \"url\": \"https://cdn.creatomate.com/video.mp4\", \"duration\": 28.5}");

        _httpHandler.SetupBinaryResponse(
            "https://cdn.creatomate.com/video.mp4",
            new byte[] { 0x00, 0x00, 0x00, 0x1C }); // fake MP4 bytes

        var result = await _sut.GenerateVideoAsync("Script text", "Test Title");

        Assert.True(result.IsSuccess);
        Assert.Equal(29, result.Data!.DurationSeconds); // ceil(28.5)
        Assert.Equal("video/mp4", result.Data.ContentType);
        Assert.NotEmpty(result.Data.VideoData);
    }

    [Fact]
    public async Task GenerateVideoAsync_RenderFails_ReturnsFailure()
    {
        _httpHandler.SetupJsonResponse(
            "https://api.creatomate.com/v1/renders",
            """[{"id": "render-fail", "status": "planned"}]""");

        _httpHandler.SetupJsonResponse(
            "https://api.creatomate.com/v1/renders/render-fail",
            """{"id": "render-fail", "status": "failed", "error_message": "Template error"}""");

        var result = await _sut.GenerateVideoAsync("Script", "Title");

        Assert.False(result.IsSuccess);
        Assert.Contains("Template error", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateVideoAsync_ApiReturnsError_ReturnsFailure()
    {
        _httpHandler.SetupErrorResponse(
            "https://api.creatomate.com/v1/renders",
            HttpStatusCode.Unauthorized);

        var result = await _sut.GenerateVideoAsync("Script", "Title");

        Assert.False(result.IsSuccess);
        Assert.Contains("Unauthorized", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateVideoAsync_RequestContainsSourceNotTemplateId()
    {
        _httpHandler.SetupJsonResponse(
            "https://api.creatomate.com/v1/renders",
            """[{"id": "render-src", "status": "planned"}]""");

        _httpHandler.SetupJsonResponse(
            "https://api.creatomate.com/v1/renders/render-src",
            """{"id": "render-src", "status": "succeeded", "url": "https://cdn.creatomate.com/v.mp4", "duration": 30}""");

        _httpHandler.SetupBinaryResponse(
            "https://cdn.creatomate.com/v.mp4",
            new byte[] { 0x01 });

        await _sut.GenerateVideoAsync("Script", "Title");

        var requestBody = _httpHandler.LastRequestBody;
        Assert.NotNull(requestBody);
        Assert.Contains("source", requestBody);
        Assert.DoesNotContain("template_id", requestBody);
    }

    [Fact]
    public async Task GenerateVideoAsync_NoApiKey_ReturnsFailure_WithoutThrowing()
    {
        var config = Substitute.For<IConfiguration>();
        config["CreatomateApiKey"].Returns((string?)null);
        var sut = new CreatomateVideoGenerationService(
            new HttpClient(), config, NullLogger<CreatomateVideoGenerationService>.Instance);

        var result = await sut.GenerateVideoAsync("script", "title");

        Assert.False(result.IsSuccess);
        Assert.Contains("CreatomateApiKey", result.ErrorMessage);
    }

    internal class FakeHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (byte[] content, string contentType, HttpStatusCode status)> _responses = new();
        public string? LastRequestBody { get; private set; }

        public void SetupJsonResponse(string url, string json)
        {
            _responses[NormalizeUrl(url)] = (Encoding.UTF8.GetBytes(json), "application/json", HttpStatusCode.OK);
        }

        public void SetupBinaryResponse(string url, byte[] data)
        {
            _responses[NormalizeUrl(url)] = (data, "application/octet-stream", HttpStatusCode.OK);
        }

        public void SetupErrorResponse(string url, HttpStatusCode status)
        {
            _responses[NormalizeUrl(url)] = (Encoding.UTF8.GetBytes("error"), "text/plain", status);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content != null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            var url = NormalizeUrl(request.RequestUri!.ToString());

            if (_responses.TryGetValue(url, out var setup))
            {
                return new HttpResponseMessage(setup.status)
                {
                    Content = new ByteArrayContent(setup.content)
                    {
                        Headers = { { "Content-Type", setup.contentType } },
                    },
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static string NormalizeUrl(string url) => url.TrimEnd('/');
    }
}
