namespace ECommerce.Application.Abstractions.Persistence;

public sealed record ExpiredOrderCandidate(Guid OrderId, DateTimeOffset ExpiresAt);
