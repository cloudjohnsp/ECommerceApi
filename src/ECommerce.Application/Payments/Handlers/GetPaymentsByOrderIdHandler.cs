using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Payments.Dtos;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Payments.Handlers;

public sealed class GetPaymentsByOrderIdHandler(
    IPaymentRepository paymentRepository,
    IOrderRepository orderRepository)
    : IRequestHandler<GetPaymentsByOrderIdQuery, Result<IReadOnlyCollection<PaymentDto>>>
{
    public async Task<Result<IReadOnlyCollection<PaymentDto>>> Handle(
        GetPaymentsByOrderIdQuery request,
        CancellationToken cancellationToken)
    {
        if (request.CustomerId is { } customerId)
        {
            var order = await orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
            if (order is null || order.CustomerId != customerId)
                return Result<IReadOnlyCollection<PaymentDto>>.Failure("Payment not found.");
        }

        var payments = await paymentRepository.GetByOrderIdAsync(request.OrderId, cancellationToken);
        return payments.Count == 0
            ? Result<IReadOnlyCollection<PaymentDto>>.Failure("Payment not found.")
            : Result<IReadOnlyCollection<PaymentDto>>.Success(
                payments.Select(payment => payment.ToDto()).ToArray());
    }
}
