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

namespace ECommerce.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        services.AddJwtAuthentication(configuration);
        services.AddRabbitMq(configuration);
        services.AddRedisCache(configuration);
        services.AddPaymentGateway(configuration);
        services.AddPaymentOutboxProcessor(configuration);

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
            .ValidateOnStart();

        var options = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>()
            ?? new RedisOptions();
        if (!options.Enabled)
        {
            services.AddSingleton<IProductCache, NullProductCache>();
            return services;
        }

        services.AddStackExchangeRedisCache(redis =>
        {
            redis.Configuration = options.Configuration;
            redis.InstanceName = options.InstanceName;
        });
        services.AddScoped<IProductCache, RedisProductCache>();
        services.AddHealthChecks()
            .AddCheck<RedisHealthCheck>(
                "redis",
                tags: ["ready"],
                timeout: TimeSpan.FromSeconds(5));

        return services;
    }

    private static IServiceCollection AddPaymentOutboxProcessor(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<OutboxProcessorOptions>()
            .Bind(configuration.GetSection(OutboxProcessorOptions.SectionName))
            .Validate(options => options.PollingIntervalSeconds > 0,
                "OutboxProcessor:PollingIntervalSeconds must be greater than zero.")
            .Validate(options => options.BatchSize > 0,
                "OutboxProcessor:BatchSize must be greater than zero.")
            .ValidateOnStart();

        services.AddHostedService<PaymentOutboxWorker>();
        return services;
    }

    private static IServiceCollection AddPaymentGateway(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<PaymentGatewayOptions>()
            .Bind(configuration.GetSection(PaymentGatewayOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
                "PaymentGateway:BaseUrl must be an absolute URL.")
            .Validate(options => Uri.TryCreate(options.CallbackUrl, UriKind.Absolute, out _),
                "PaymentGateway:CallbackUrl must be an absolute URL.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.WebhookSecret),
                "PaymentGateway:WebhookSecret is required.")
            .Validate(options => options.TimeoutSeconds > 0,
                "PaymentGateway:TimeoutSeconds must be greater than zero.")
            .ValidateOnStart();

        services.AddHttpClient<IPaymentGateway, PaymentGatewayClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PaymentGatewayOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });
        services.AddHttpClient(PaymentGatewayHealthCheck.HttpClientName, (serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PaymentGatewayOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });
        services.AddHealthChecks()
            .AddCheck<PaymentGatewayHealthCheck>(
                "payment-gateway",
                tags: ["ready"],
                timeout: TimeSpan.FromSeconds(5));
        services.AddSingleton<IPaymentWebhookSignatureVerifier, PaymentWebhookSignatureVerifier>();

        return services;
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
        services.AddHostedService<IntegrationEventOutboxWorker>();
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
            .Validate(options => !string.IsNullOrWhiteSpace(options.SecretKey), "Jwt:SecretKey is required.")
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
