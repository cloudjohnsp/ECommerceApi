using ECommerce.Application.Payments.Dtos;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Payments;

public sealed record GetPaymentsByOrderIdQuery(Guid OrderId, Guid? CustomerId = null)
    : IRequest<Result<IReadOnlyCollection<PaymentDto>>>;
