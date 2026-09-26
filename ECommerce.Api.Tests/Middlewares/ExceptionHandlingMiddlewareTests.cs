using System.Net;
using System.Text.Json;
using ECommerce.Api.Middlewares;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace ECommerce.Api.Tests.Middlewares;

public sealed class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ValidationException_ReturnsStructuredProblemWithRequestIdentifiers()
    {
        var exception = new ValidationException(
        [
            new ValidationFailure("Email", "E-mail is required."),
            new ValidationFailure("Email", "E-mail is required."),
            new ValidationFailure("Password", "Password is too short.")
        ]);
        var context = CreateContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "checkout-123";
        var middleware = CreatePipeline(_ => throw exception);

        await middleware.InvokeAsync(context);

        using var document = await ReadResponseAsync(context);
        var root = document.RootElement;
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        context.Response.ContentType.Should().StartWith("application/problem+json");
        root.GetProperty("title").GetString().Should().Be("One or more validation errors occurred.");
        root.GetProperty("instance").GetString().Should().Be("/test");
        root.GetProperty("errors").GetProperty("Email").EnumerateArray()
            .Select(item => item.GetString()).Should().ContainSingle("E-mail is required.");
        root.GetProperty("errors").GetProperty("Password").EnumerateArray()
            .Select(item => item.GetString()).Should().ContainSingle("Password is too short.");
        root.GetProperty("traceId").GetString().Should().Be("trace-123");
        root.GetProperty("correlationId").GetString().Should().Be("checkout-123");
    }

    [Fact]
    public async Task InvokeAsync_UnexpectedException_ReturnsSanitizedProblemWithRequestIdentifiers()
    {
        var context = CreateContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "checkout-456";
        var middleware = CreatePipeline(_ => throw new InvalidOperationException("database-password-secret"));

        await middleware.InvokeAsync(context);

        using var document = await ReadResponseAsync(context);
        var root = document.RootElement;
        var responseBody = root.GetRawText();
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        context.Response.ContentType.Should().StartWith("application/problem+json");
        root.GetProperty("title").GetString().Should().Be("An unexpected error occurred.");
        root.GetProperty("traceId").GetString().Should().Be("trace-123");
        root.GetProperty("correlationId").GetString().Should().Be("checkout-456");
        responseBody.Should().NotContain("database-password-secret");
        responseBody.Should().NotContain(nameof(InvalidOperationException));
    }

    private static CorrelationIdMiddleware CreatePipeline(RequestDelegate terminal)
    {
        var exceptionMiddleware = new ExceptionHandlingMiddleware(
            terminal,
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        return new CorrelationIdMiddleware(
            exceptionMiddleware.InvokeAsync,
            NullLogger<CorrelationIdMiddleware>.Instance);
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.TraceIdentifier = "trace-123";
        context.Request.Path = "/test";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<JsonDocument> ReadResponseAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await JsonDocument.ParseAsync(context.Response.Body);
    }
}
