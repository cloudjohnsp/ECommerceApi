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

public sealed class SearchOrdersValidator : AbstractValidator<SearchOrdersQuery>
{
    private static readonly string[] AllowedSortFields = ["createdAt", "status", "total"];

    public SearchOrdersValidator()
    {
        RuleFor(query => query.CustomerId).NotEmpty().When(query => query.CustomerId.HasValue);
        RuleFor(query => query.Status).IsInEnum().When(query => query.Status.HasValue);
        RuleFor(query => query.CreatedToUtc)
            .GreaterThan(query => query.CreatedFromUtc)
            .When(query => query.CreatedFromUtc.HasValue && query.CreatedToUtc.HasValue)
            .WithMessage("CreatedToUtc must be greater than CreatedFromUtc.");
        RuleFor(query => query.SortBy)
            .Must(sortBy => AllowedSortFields.Contains(sortBy, StringComparer.OrdinalIgnoreCase))
            .WithMessage("SortBy must be one of: createdAt, status, total.");
        RuleFor(query => query.Page).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class AddOrderItemValidator : AbstractValidator<AddOrderItemCommand>
{
    public AddOrderItemValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
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
