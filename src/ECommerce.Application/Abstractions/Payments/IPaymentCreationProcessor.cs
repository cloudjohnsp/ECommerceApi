using ECommerce.Domain.Entities;
using ECommerce.Shared.Results;

namespace ECommerce.Application.Abstractions.Payments;

public interface IPaymentCreationProcessor
{
    Task<Result<Payment>> ProcessAsync(
        Guid outboxMessageId,
        CancellationToken cancellationToken = default);
}
