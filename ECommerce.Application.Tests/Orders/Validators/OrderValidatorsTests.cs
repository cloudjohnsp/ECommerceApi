using ECommerce.Application.Orders;
using ECommerce.Application.Orders.Validators;
using ECommerce.Domain.Enums;
using FluentAssertions;

namespace ECommerce.Application.Tests.Orders.Validators;

public sealed class OrderValidatorsTests
{
    [Fact]
    public async Task Create_WithValidItems_IsValid()
    {
        var command = new CreateOrderCommand(Guid.NewGuid(), [new CreateOrderItem(Guid.NewGuid(), 2)]);
        (await new CreateOrderValidator().ValidateAsync(command)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Create_WithDuplicateProducts_IsInvalid()
    {
        var productId = Guid.NewGuid();
        var command = new CreateOrderCommand(Guid.NewGuid(),
            [new CreateOrderItem(productId, 1), new CreateOrderItem(productId, 2)]);

        var result = await new CreateOrderValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorMessage == "An order cannot contain duplicate products.");
    }

    [Fact]
    public async Task Update_WithCancelledStatus_IsInvalid()
    {
        var result = await new UpdateOrderValidator()
            .ValidateAsync(new UpdateOrderCommand(Guid.NewGuid(), OrderStatus.Cancelled));

        result.IsValid.Should().BeFalse();
    }
}
