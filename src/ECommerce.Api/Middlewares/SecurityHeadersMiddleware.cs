namespace ECommerce.Api.Middlewares;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers.Append("Referrer-Policy", "no-referrer");
            headers.Append("Permissions-Policy", "camera=(), geolocation=(), microphone=()");

            return Task.CompletedTask;
        });

        await next(context);
    }
}
