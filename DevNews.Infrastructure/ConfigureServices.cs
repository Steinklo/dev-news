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

        // Crawl service
        services.AddHttpClient<ICrawlService, AiCrawlService>();

        // Anthropic AI service
        services.AddSingleton<IAiService, AnthropicAiService>();

        // AI-powered services
        services.AddScoped<ICurationService, AiCurationService>();
        services.AddScoped<IDuplicationService, AiDuplicationService>();

        return services;
    }
}