using ECommerce.Domain.Enums;
using FluentValidation;

namespace ECommerce.Application.Orders.Validators;

public sealed class CreateOrderValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.ProductId).NotEmpty();
            item.RuleFor(x => x.Quantity).GreaterThan(0);
        });
        RuleFor(x => x.Items)
            .Must(items => items is null || items.Select(i => i.ProductId).Distinct().Count() == items.Count)
            .WithMessage("An order cannot contain duplicate products.");
    }
}

public sealed class GetOrderByIdValidator : AbstractValidator<GetOrderByIdQuery>
{
    public GetOrderByIdValidator() => RuleFor(x => x.OrderId).NotEmpty();
}

public sealed class UpdateOrderValidator : AbstractValidator<UpdateOrderCommand>
{
    public UpdateOrderValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Status).IsInEnum().Equal(OrderStatus.Paid)
            .WithMessage("Only transition to Paid is supported. Use DELETE to cancel an order.");
    }
}

public sealed class DeleteOrderValidator : AbstractValidator<DeleteOrderCommand>
{
    public DeleteOrderValidator() => RuleFor(x => x.OrderId).NotEmpty();
}
