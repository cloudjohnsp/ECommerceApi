using System.Text;
using ECommerce.Application.Abstractions.Security;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.Options;
using ECommerce.Infrastructure.Payments;
using ECommerce.Infrastructure.Security;
using ECommerce.Infrastructure.Messaging;
using ECommerce.Application.Abstractions.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using RabbitMQ.Client;
using ECommerce.Infrastructure.HealthChecks;
using ECommerce.Application.Abstractions.Caching;
using ECommerce.Infrastructure.Caching;
using ECommerce.Application.Abstractions.Storage;
using ECommerce.Infrastructure.Storage;
using Azure.Storage.Blobs;
using ECommerce.Application.Abstractions.Email;
using ECommerce.Infrastructure.Email;
using MimeKit;
using ECommerce.Application.Abstractions.Features;
using ECommerce.Infrastructure.Features;
using Hangfire;
using Hangfire.PostgreSql;
using ECommerce.Application.Abstractions.Orders;
using ECommerce.Infrastructure.BackgroundJobs;

namespace ECommerce.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IUserActionTokenService, UserActionTokenService>();
        services.AddSingleton<IFeatureFlagService, ConfigurationFeatureFlagService>();

        services.AddJwtAuthentication(configuration);
        services.AddRabbitMq(configuration);
        services.AddRedisCache(configuration);
        services.AddProductImageStorage(configuration);
        services.AddEmailDelivery(configuration);
        services.AddPaymentGateway(configuration);
        services.AddOutboxProcessing(configuration);

        return services;
    }

    private static IServiceCollection AddEmailDelivery(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Host),
                "Email:Host is required when e-mail delivery is enabled.")
            .Validate(options => !options.Enabled || options.Port is > 0 and <= 65535,
                "Email:Port must be between 1 and 65535.")
            .Validate(options => !options.Enabled ||
                    MailboxAddress.TryParse(options.FromAddress, out _),
                "Email:FromAddress must be a valid e-mail address.")
            .Validate(options => !options.Enabled ||
                    Uri.TryCreate(options.PublicAppBaseUrl, UriKind.Absolute, out _),
                "Email:PublicAppBaseUrl must be an absolute URL.")
            .ValidateOnStart();

        var options = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>()
            ?? new EmailOptions();
        if (options.Enabled)
        {
            services.AddScoped<IUserEmailSender, SmtpUserEmailSender>();
            services.AddHealthChecks()
                .AddCheck<SmtpHealthCheck>(
                    "smtp",
                    tags: ["ready"],
                    timeout: TimeSpan.FromSeconds(5));
        }
        else
            services.AddSingleton<IUserEmailSender, DisabledUserEmailSender>();
        return services;
    }

    private static IServiceCollection AddProductImageStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ProductImageStorageOptions>()
            .Bind(configuration.GetSection(ProductImageStorageOptions.SectionName))
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.ConnectionString),
                "ProductImageStorage:ConnectionString is required when storage is enabled.")
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.ContainerName),
                "ProductImageStorage:ContainerName is required when storage is enabled.")
            .Validate(options => string.IsNullOrWhiteSpace(options.PublicBaseUrl) ||
                    Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out _),
                "ProductImageStorage:PublicBaseUrl must be an absolute URL.")
            .ValidateOnStart();

        var options = configuration.GetSection(ProductImageStorageOptions.SectionName)
            .Get<ProductImageStorageOptions>() ?? new ProductImageStorageOptions();
        if (!options.Enabled)
        {
            services.AddSingleton<IProductImageStorage, DisabledProductImageStorage>();
            return services;
        }

        services.AddSingleton(new BlobServiceClient(options.ConnectionString));
        services.AddScoped<IProductImageStorage, AzureBlobProductImageStorage>();
        services.AddHealthChecks()
            .AddCheck<AzureBlobStorageHealthCheck>(
                "product-image-storage",
                tags: ["ready"],
                timeout: TimeSpan.FromSeconds(5));
        return services;
    }

    private static IServiceCollection AddRedisCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName))
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Configuration),
                "Redis:Configuration is required when Redis is enabled.")
            .Validate(options => options.ProductExpirationMinutes > 0,
                "Redis:ProductExpirationMinutes must be greater than zero.")
            .Validate(options => options.CategoryExpirationMinutes > 0,
                "Redis:CategoryExpirationMinutes must be greater than zero.")
            .ValidateOnStart();

        var options = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>()
            ?? new RedisOptions();
        if (!options.Enabled)
        {
            services.AddSingleton<IProductCache, NullProductCache>();
            services.AddSingleton<ICategoryCache, NullCategoryCache>();
            return services;
        }

        services.AddStackExchangeRedisCache(redis =>
        {
            redis.Configuration = options.Configuration;
            redis.InstanceName = options.InstanceName;
        });
        services.AddScoped<IProductCache, RedisProductCache>();
        services.AddScoped<ICategoryCache, RedisCategoryCache>();
        services.AddHealthChecks()
            .AddCheck<RedisHealthCheck>(
                "redis",
                tags: ["ready"],
                timeout: TimeSpan.FromSeconds(5));

        return services;
    }

    private static IServiceCollection AddOutboxProcessing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<OutboxProcessorOptions>()
            .Bind(configuration.GetSection(OutboxProcessorOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.CronExpression),
                "OutboxProcessor:CronExpression is required.")
            .Validate(options => options.BatchSize > 0,
                "OutboxProcessor:BatchSize must be greater than zero.")
            .Validate(options => options.WorkerCount > 0,
                "OutboxProcessor:WorkerCount must be greater than zero.")
            .ValidateOnStart();

        services.AddOptions<OrderExpirationOptions>()
            .Bind(configuration.GetSection(OrderExpirationOptions.SectionName))
            .Validate(options => options.PaymentLifetimeMinutes > 0,
                "OrderExpiration:PaymentLifetimeMinutes must be greater than zero.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.CronExpression),
                "OrderExpiration:CronExpression is required.")
            .Validate(options => options.BatchSize > 0,
                "OrderExpiration:BatchSize must be greater than zero.")
            .ValidateOnStart();

        services.AddScoped<PaymentOutboxJob>();
        services.AddScoped<UserEmailOutboxJob>();
        services.AddScoped<IntegrationEventOutboxJob>();
        services.AddScoped<OrderExpirationJob>();
        services.AddSingleton<IOrderExpirationPolicy, ConfiguredOrderExpirationPolicy>();

        var options = configuration.GetSection(OutboxProcessorOptions.SectionName)
            .Get<OutboxProcessorOptions>() ?? new OutboxProcessorOptions();
        var expirationOptions = configuration.GetSection(OrderExpirationOptions.SectionName)
            .Get<OrderExpirationOptions>() ?? new OrderExpirationOptions();
        if (!options.Enabled && !expirationOptions.Enabled)
            return services;

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required for Hangfire.");

        services.AddHangfire(hangfire => hangfire
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                storage => storage.UseNpgsqlConnection(connectionString),
                new PostgreSqlStorageOptions
                {
                    SchemaName = "hangfire",
                    PrepareSchemaIfNecessary = true
                }));
        services.AddHangfireServer(server => server.WorkerCount = options.WorkerCount);
        return services;
    }

    private static IServiceCollection AddPaymentGateway(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<PaymentGatewayOptions>()
            .Bind(configuration.GetSection(PaymentGatewayOptions.SectionName))
            .Validate(options => IsHttpUrl(options.BaseUrl, allowQuery: false),
                "PaymentGateway:BaseUrl must be an absolute HTTP or HTTPS URL without credentials, query, or fragment.")
            .Validate(options => IsHttpUrl(options.CallbackUrl, allowQuery: true),
                "PaymentGateway:CallbackUrl must be an absolute HTTP or HTTPS URL without credentials or fragment.")
            .Validate(options => Encoding.UTF8.GetByteCount(options.WebhookSecret) >= 32,
                "PaymentGateway:WebhookSecret must contain at least 32 UTF-8 bytes.")
            .Validate(options => options.TimeoutSeconds > 0,
                "PaymentGateway:TimeoutSeconds must be greater than zero.")
            .ValidateOnStart();

        services.AddHttpClient<IPaymentGateway, PaymentGatewayClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PaymentGatewayOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        }).ConfigurePrimaryHttpMessageHandler(PaymentGatewayHttpMessageHandlerFactory.Create);
        services.AddHttpClient(PaymentGatewayHealthCheck.HttpClientName, (serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PaymentGatewayOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        }).ConfigurePrimaryHttpMessageHandler(PaymentGatewayHttpMessageHandlerFactory.Create);
        services.AddHealthChecks()
            .AddCheck<PaymentGatewayHealthCheck>(
                "payment-gateway",
                tags: ["ready"],
                timeout: TimeSpan.FromSeconds(5));
        services.AddSingleton<IPaymentWebhookSignatureVerifier, PaymentWebhookSignatureVerifier>();

        return services;
    }

    private static bool IsHttpUrl(string value, bool allowQuery)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;

        return (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
               !string.IsNullOrWhiteSpace(uri.Host) &&
               string.IsNullOrEmpty(uri.UserInfo) &&
               string.IsNullOrEmpty(uri.Fragment) &&
               (allowQuery || string.IsNullOrEmpty(uri.Query));
    }

    private static IServiceCollection AddRabbitMq(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.HostName), "RabbitMq:HostName is required.")
            .Validate(options => options.Port > 0, "RabbitMq:Port must be greater than zero.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.UserName), "RabbitMq:UserName is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Password), "RabbitMq:Password is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.VirtualHost), "RabbitMq:VirtualHost is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ExchangeName), "RabbitMq:ExchangeName is required.")
            .ValidateOnStart();

        var rabbitMqOptions = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
            ?? throw new InvalidOperationException("RabbitMq configuration section is missing.");

        services.AddSingleton(_ => new ConnectionFactory
        {
            HostName = rabbitMqOptions.HostName,
            Port = rabbitMqOptions.Port,
            UserName = rabbitMqOptions.UserName,
            Password = rabbitMqOptions.Password,
            VirtualHost = rabbitMqOptions.VirtualHost,
            ClientProvidedName = rabbitMqOptions.ClientProvidedName,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true
        });
        services.AddSingleton<IIntegrationEventPublisher, RabbitMqEventPublisher>();
        services.AddHealthChecks()
            .AddCheck<RabbitMqHealthCheck>(
                "rabbitmq",
                tags: ["ready"],
                timeout: TimeSpan.FromSeconds(5));

        return services;
    }

    private static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => Encoding.UTF8.GetByteCount(options.SecretKey) >= 32,
                "Jwt:SecretKey must contain at least 32 UTF-8 bytes.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Jwt:Issuer is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Jwt:Audience is required.")
            .Validate(options => options.ExpirationMinutes > 0, "Jwt:ExpirationMinutes must be greater than zero.")
            .Validate(options => options.RefreshTokenExpirationDays > 0, "Jwt:RefreshTokenExpirationDays must be greater than zero.")
            .ValidateOnStart();

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt configuration section is missing.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        services.AddAuthorization();

        return services;
    }
}
