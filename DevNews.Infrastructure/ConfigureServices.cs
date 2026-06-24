using Azure.Storage.Blobs;
using DevNews.Application.Common.Repositories;
using DevNews.Application.Common.Services;
using DevNews.Infrastructure.Repositories;
using DevNews.Infrastructure.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DevNews.Infrastructure;

public static class ConfigureServices
{
    private const string DatabaseId = "dev-news-db";
    private const string ContainerId = "news-items";
    private const string ShortVideoContainerId = "short-videos";
    private const string SocialPostContainerId = "text-posts";

    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Cosmos DB
        services.AddSingleton<CosmosClient>(_ =>
            new CosmosClient(
                configuration["CosmosDbEndpoint"],
                configuration["CosmosDbKey"]));

        // Repositories
        services.AddScoped<INewsItemRepository>(sp =>
        {
            var cosmosClient = sp.GetRequiredService<CosmosClient>();
            return new NewsItemCosmosRepository(cosmosClient, DatabaseId, ContainerId);
        });

        services.AddScoped<IShortVideoRepository>(sp =>
        {
            var cosmosClient = sp.GetRequiredService<CosmosClient>();
            return new ShortVideoCosmosRepository(cosmosClient, DatabaseId, ShortVideoContainerId);
        });

        services.AddScoped<ISocialPostRepository>(sp =>
        {
            var cosmosClient = sp.GetRequiredService<CosmosClient>();
            return new SocialPostCosmosRepository(cosmosClient, DatabaseId, SocialPostContainerId);
        });

        // Azure Blob Storage
        services.AddSingleton(_ =>
            new BlobServiceClient(configuration["AzureStorageConnectionString"]));
        services.AddScoped<IVideoStorageService, AzureBlobVideoStorageService>();

        // Crawl service
        services.AddHttpClient<ICrawlService, AiCrawlService>();

        // Anthropic AI service
        services.AddSingleton<IAiService, AnthropicAiService>();

        // AI-powered services
        services.AddScoped<ICurationService, AiCurationService>();
        services.AddScoped<IDuplicationService, AiDuplicationService>();

        // Video generation services
        services.AddScoped<IVideoScriptService, AiVideoScriptService>();
        services.AddScoped<IVideoScriptValidationService, AiVideoScriptValidationService>();
        services.AddHttpClient<IVideoGenerationService, CreatomateVideoGenerationService>();

        // Social post services
        services.AddScoped<ISocialPostGenerationService, AiSocialPostService>();
        // Multiple text-post platforms — social posts fan out to every one that is configured.
        services.AddHttpClient<ISocialPostPublisher, SocialPostPublisher>();
        services.AddHttpClient<ISocialPostPublisher, BlueskyPublisher>();

        // Platform publishing services
        services.AddHttpClient<YouTubePublishingService>();
        services.AddHttpClient<LinkedInPublishingService>();
        services.AddScoped<IPlatformPublishingService, PlatformPublishingRouter>();

        return services;
    }
}