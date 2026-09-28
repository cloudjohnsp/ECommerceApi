using ECommerce.Api.Middlewares;
using ECommerce.Application.Abstractions.Observability;

namespace ECommerce.Api.Observability;

public sealed class HttpCorrelationContext(IHttpContextAccessor httpContextAccessor)
    : ICorrelationContext
{
    public string? CorrelationId =>
        httpContextAccessor.HttpContext?.Items[CorrelationIdMiddleware.ContextItemName] as string;
}
