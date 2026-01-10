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
        services.AddScoped<INewsItemRepository, NewsItemCosmosRepository>();

        // Crawl service with options
        services.Configure<CrawlServiceOptions>(
            configuration.GetSection(CrawlServiceOptions.SectionName));
        services.AddHttpClient<ICrawlService, ArticleCrawlService>();

        // Anthropic AI service
        services.Configure<AnthropicOptions>(
            configuration.GetSection(AnthropicOptions.SectionName));
        services.AddSingleton<IAiService, AnthropicAiService>();

        // AI-powered services (depend on IAiService)
        services.AddScoped<ICurationService, AiCurationService>();
        services.AddScoped<IDuplicationService, AiDuplicationService>();

        return services;
    }
}