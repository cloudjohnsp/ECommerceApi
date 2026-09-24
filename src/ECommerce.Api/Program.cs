using ECommerce.Application;
using ECommerce.Api.Middlewares;
using ECommerce.Api.OpenApi;
using ECommerce.Infrastructure;
using ECommerce.Persistence;
using ECommerce.Api;
using ECommerce.Api.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApi(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();
app.UseCors(ApiCorsPolicy.Name);
app.MapOpenApi();
app.UseSwaggerUI(opt => { opt.SwaggerEndpoint("/openapi/v1.json", "v1"); });

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/api/health");
app.MapHealthChecks("/api/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/api/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});

app.Run();

public partial class Program;
