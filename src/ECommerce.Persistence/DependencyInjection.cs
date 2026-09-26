using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Persistence.Contexts;
using ECommerce.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ECommerce.Persistence.HealthChecks;
using ECommerce.Persistence.Options;
using ECommerce.Persistence.Seeding;
using ECommerce.Domain.ValueObjects;

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

        services.AddOptions<DatabaseSeedOptions>()
            .Bind(configuration.GetSection(DatabaseSeedOptions.SectionName))
            .Validate(options =>
                    string.IsNullOrWhiteSpace(options.AdministratorEmail) ==
                    string.IsNullOrWhiteSpace(options.AdministratorPassword),
                "DatabaseSeed administrator e-mail and password must be configured together.")
            .Validate(options =>
                    string.IsNullOrWhiteSpace(options.AdministratorPassword) ||
                    options.AdministratorPassword.Length >= 12,
                "DatabaseSeed:AdministratorPassword must contain at least 12 characters.")
            .Validate(options =>
                    string.IsNullOrWhiteSpace(options.AdministratorEmail) ||
                    Email.Create(options.AdministratorEmail).IsSuccess,
                "DatabaseSeed:AdministratorEmail must be a valid e-mail address.")
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
        services.AddScoped<IUserAuditRepository, UserAuditRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductImageRepository, ProductImageRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IAdminReportingRepository, AdminReportingRepository>();
        services.AddScoped<DatabaseSeeder>();
        return services;
    }
}
