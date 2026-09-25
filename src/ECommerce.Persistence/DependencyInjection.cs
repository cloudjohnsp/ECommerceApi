using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Infrastructure.Persistence;
using ECommerce.Persistence.Contexts;
using ECommerce.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ECommerce.Persistence.HealthChecks;
using ECommerce.Persistence.Options;

namespace ECommerce.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        services.AddOptions<DatabaseInitializationOptions>()
            .Bind(configuration.GetSection(DatabaseInitializationOptions.SectionName))
            .Validate(options => options.MaxAttempts > 0,
                "DatabaseInitialization:MaxAttempts must be greater than zero.")
            .Validate(options => options.RetryDelaySeconds > 0,
                "DatabaseInitialization:RetryDelaySeconds must be greater than zero.")
            .ValidateOnStart();

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>(
                "postgres",
                tags: ["ready"],
                timeout: TimeSpan.FromSeconds(5));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUserActionTokenRepository, UserActionTokenRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductImageRepository, ProductImageRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IAdminReportingRepository, AdminReportingRepository>();
        return services;
    }
}
