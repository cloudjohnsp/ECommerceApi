using ECommerce.Api.OpenApi;
using ECommerce.Application;
using Asp.Versioning;
using ECommerce.Infrastructure;
using ECommerce.Persistence;
using ECommerce.Api.Options;
using ECommerce.Api.Security;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Security.Claims;
using System.Threading.RateLimiting;
using ECommerce.Infrastructure.BackgroundJobs;

namespace ECommerce.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        }).AddMvc();
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
        services.AddApiProtection(configuration);
        services.AddApiObservability(configuration);
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddPersistence(configuration);

        return services;
    }

    private static IServiceCollection AddApiObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ObservabilityOptions>()
            .Bind(configuration.GetSection(ObservabilityOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ServiceName),
                "Observability:ServiceName is required.")
            .Validate(options => string.IsNullOrWhiteSpace(options.OtlpEndpoint) ||
                    Uri.TryCreate(options.OtlpEndpoint, UriKind.Absolute, out _),
                "Observability:OtlpEndpoint must be an absolute URI when configured.")
            .ValidateOnStart();

        var options = configuration.GetSection(ObservabilityOptions.SectionName)
            .Get<ObservabilityOptions>() ?? new ObservabilityOptions();

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(options.ServiceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(instrumentation =>
                    {
                        instrumentation.Filter = context =>
                            !context.Request.Path.StartsWithSegments("/metrics");
                    })
                    .AddHttpClientInstrumentation();

                if (TryGetOtlpEndpoint(options, out var endpoint))
                    tracing.AddOtlpExporter(exporter => exporter.Endpoint = endpoint);
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(OrderExpirationJob.MeterName);

                if (options.EnablePrometheus)
                    metrics.AddPrometheusExporter();

                if (TryGetOtlpEndpoint(options, out var endpoint))
                    metrics.AddOtlpExporter(exporter => exporter.Endpoint = endpoint);
            });

        return services;
    }

    private static bool TryGetOtlpEndpoint(
        ObservabilityOptions options,
        out Uri endpoint) =>
        Uri.TryCreate(options.OtlpEndpoint, UriKind.Absolute, out endpoint!);

    private static IServiceCollection AddApiProtection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ApiProtectionOptions>()
            .Bind(configuration.GetSection(ApiProtectionOptions.SectionName))
            .Validate(options => options.AllowedOrigins.Length > 0,
                "ApiProtection:AllowedOrigins must contain at least one origin.")
            .Validate(options => options.AllowedOrigins.All(origin =>
                    Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)),
                "ApiProtection:AllowedOrigins must contain absolute HTTP or HTTPS origins.")
            .Validate(options => options.GlobalPermitLimit > 0,
                "ApiProtection:GlobalPermitLimit must be greater than zero.")
            .Validate(options => options.AuthenticationPermitLimit > 0,
                "ApiProtection:AuthenticationPermitLimit must be greater than zero.")
            .Validate(options => options.WindowSeconds > 0,
                "ApiProtection:WindowSeconds must be greater than zero.")
            .ValidateOnStart();

        var options = configuration.GetSection(ApiProtectionOptions.SectionName).Get<ApiProtectionOptions>()
            ?? new ApiProtectionOptions();

        services.AddCors(cors =>
        {
            cors.AddPolicy(ApiCorsPolicy.Name, policy =>
                policy.WithOrigins(options.AllowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod());
        });
        if (options.UseForwardedHeaders)
        {
            services.Configure<ForwardedHeadersOptions>(forwardedHeaders =>
            {
                forwardedHeaders.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                forwardedHeaders.ForwardLimit = 1;
                forwardedHeaders.KnownIPNetworks.Clear();
                forwardedHeaders.KnownProxies.Clear();
            });
        }
        services.AddRateLimiter(rateLimiter =>
        {
            rateLimiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            rateLimiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                CreatePartition(context, options.GlobalPermitLimit, options.WindowSeconds, "global"));
            rateLimiter.AddPolicy(ApiRateLimitPolicy.Authentication, context =>
                CreatePartition(context, options.AuthenticationPermitLimit, options.WindowSeconds, "authentication"));
        });

        return services;
    }

    private static RateLimitPartition<string> CreatePartition(
        HttpContext context,
        int permitLimit,
        int windowSeconds,
        string policyName)
    {
        var clientId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            $"{policyName}:{clientId}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    }
}
