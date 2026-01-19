using Azure.Storage.Blobs;
using DevNews.Application.Common.Repositories;
using DevNews.Application.Common.Services;
using DevNews.Infrastructure.Repositories;
using DevNews.Infrastructure.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevNews.Infrastructure;

public static class ConfigureServices
{
    private const string DatabaseId = "dev-news-db";
    private const string ContainerId = "news-items";
    private const string VideoContainerId = "video-content";

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

        services.AddScoped<IVideoContentRepository>(sp =>
        {
            var cosmosClient = sp.GetRequiredService<CosmosClient>();
            var logger = sp.GetRequiredService<ILogger<VideoContentCosmosRepository>>();
            return new VideoContentCosmosRepository(cosmosClient, DatabaseId, VideoContainerId, logger);
        });

        // Crawl service
        services.AddHttpClient<ICrawlService, AiCrawlService>();

        // Anthropic AI service
        services.AddSingleton<IAiService, AnthropicAiService>();

        // AI-powered services
        services.AddScoped<ICurationService, AiCurationService>();
        services.AddScoped<IDuplicationService, AiDuplicationService>();

        // Video services
        services.AddScoped<IVideoScriptGenerator, AiVideoScriptGenerator>();
        services.AddScoped<IVideoGenerator, VideoGeneratorService>();
        services.AddScoped<IVideoValidationService, AiVideoValidationService>();
        services.AddScoped<IVideoPublishingService, VideoPublishingService>();

        // Azure Blob Storage for videos
        services.AddSingleton(sp =>
        {
            var connectionString = configuration["AzureBlobStorage:ConnectionString"]
                ?? throw new InvalidOperationException("AzureBlobStorage:ConnectionString not configured");
            return new BlobServiceClient(connectionString);
        });
        services.AddScoped<IVideoBlobStorage, VideoBlobStorageService>();

        return services;
    }
}