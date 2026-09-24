using ECommerce.Api.OpenApi;
using ECommerce.Application;
using ECommerce.Infrastructure;
using ECommerce.Persistence;
using ECommerce.Api.Options;
using ECommerce.Api.Security;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Threading.RateLimiting;

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
        services.AddApiProtection(configuration);
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddPersistence(configuration);

        return services;
    }

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
