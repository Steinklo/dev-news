using DevNews.Application.Common.Repositories;
using DevNews.Infrastructure.Repositories;
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
        services.AddSingleton<CosmosClient>(_ =>
            new CosmosClient(
                configuration["CosmosDbEndpoint"],
                configuration["CosmosDbKey"]));

        // Repositories
        services.AddScoped<INewsItemRepository, NewsItemCosmosRepository>();
        return services;
    }
}