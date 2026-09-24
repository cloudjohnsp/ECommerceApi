using System.Diagnostics;

namespace ECommerce.Api.Middlewares;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    private const int MaxCorrelationIdLength = 128;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetCorrelationId(context.Request);
        var activity = Activity.Current;

        activity?.SetTag("correlation.id", correlationId);
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        var scope = new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        };
        if (activity is not null)
        {
            scope["TraceId"] = activity.TraceId.ToString();
            scope["SpanId"] = activity.SpanId.ToString();
        }

        using (logger.BeginScope(scope))
        {
            await next(context);
        }
    }

    private static string GetCorrelationId(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(HeaderName, out var values) || values.Count != 1)
            return CreateCorrelationId();

        var candidate = values[0];
        if (string.IsNullOrWhiteSpace(candidate) ||
            candidate.Length > MaxCorrelationIdLength ||
            candidate.Any(character => !IsSafeCharacter(character)))
        {
            return CreateCorrelationId();
        }

        return candidate;
    }

    private static bool IsSafeCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.';

    private static string CreateCorrelationId() => Guid.NewGuid().ToString("N");
}
