using ECommerce.Api.OpenApi;
using ECommerce.Application;
using ECommerce.Infrastructure;
using ECommerce.Persistence;

namespace ECommerce.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.Configure<RouteOptions>(options =>
        {
            options.LowercaseUrls = true;
            options.LowercaseQueryStrings = true;
        });
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
        });
        services.AddHealthChecks();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddPersistence(configuration);

        return services;
    }
}
