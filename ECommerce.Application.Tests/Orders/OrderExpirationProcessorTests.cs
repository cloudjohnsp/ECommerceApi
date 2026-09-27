using ECommerce.Application.Abstractions.Caching;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Orders;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Tests.Support;
using ECommerce.Shared.Messaging;
using FluentAssertions;
using Moq;

namespace ECommerce.Application.Tests.Orders;

public sealed class OrderExpirationProcessorTests
{
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IInventoryReservationRepository> _reservations = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IOutboxMessageRepository> _outbox = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IProductCache> _cache = new();

    [Fact]
    public async Task ProcessBatch_ExpiredOrder_ReleasesInventoryAndWritesOutboxAtomically()
    {
        var now = new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
        var product = ProductFactory.Create(stock: 10);
        product.ReserveStock(3);
        var order = Order.Create(
            Guid.NewGuid(), now.AddHours(-1), now.AddMinutes(-30)).Value!;
        order.AddItem(product.Id, product.Name, product.Price, 3);
        var reservation = InventoryReservation.Create(
            order.Id,
            product.Id,
            product.Inventory.Id,
            3,
            order.CreatedAt,
            order.ExpiresAt).Value!;
        var locks = new List<string>();
        _orders.Setup(x => x.GetExpiredPendingCandidatesAsync(
                now, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ExpiredOrderCandidate(order.Id, order.ExpiresAt)]);
        _orders.Setup(x => x.GetByIdForUpdateAsync(order.Id, It.IsAny<CancellationToken>()))
            .Callback(() => locks.Add("order"))
            .ReturnsAsync(order);
        _reservations.Setup(x => x.GetActiveByOrderIdForUpdateAsync(
                order.Id, It.IsAny<CancellationToken>()))
            .Callback(() => locks.Add("reservations"))
            .ReturnsAsync([reservation]);
        _products.Setup(x => x.GetByIdForUpdateAsync(
                product.Id, It.IsAny<CancellationToken>()))
            .Callback(() => locks.Add("inventory"))
            .ReturnsAsync(product);
        var processor = CreateProcessor();

        var result = await processor.ProcessBatchAsync(10, now);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ExpiredCount.Should().Be(1);
        result.Value.MaximumDelaySeconds.Should().Be(1800);
        locks.Should().Equal("order", "reservations", "inventory");
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancellationReason.Should().Be(OrderCancellationReason.Expired);
        reservation.Status.Should().Be(InventoryReservationStatus.Expired);
        product.AvailableStock.Should().Be(10);
        _outbox.Verify(x => x.AddAsync(
            It.Is<OutboxMessage>(message =>
                message.Type == OutBoxMessageType.OrderCancelled &&
                message.Payload.Contains("Expired")),
            It.IsAny<CancellationToken>()), Times.Once);
        _outbox.Verify(x => x.AddAsync(
            It.Is<OutboxMessage>(message => message.Type == OutBoxMessageType.StockUpdated),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessBatch_WhenCandidateWasAlreadyCompleted_IsIdempotent()
    {
        var now = DateTimeOffset.UtcNow;
        var order = Order.Create(
            Guid.NewGuid(), now.AddHours(-1), now.AddMinutes(-30)).Value!;
        order.Cancel(OrderCancellationReason.Expired, now.AddMinutes(-20));
        _orders.Setup(x => x.GetExpiredPendingCandidatesAsync(
                now, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ExpiredOrderCandidate(order.Id, order.ExpiresAt)]);
        _orders.Setup(x => x.GetByIdForUpdateAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        var processor = CreateProcessor();

        var result = await processor.ProcessBatchAsync(10, now);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ExpiredCount.Should().Be(0);
        result.Value.SkippedCount.Should().Be(1);
        _reservations.Verify(x => x.GetActiveByOrderIdForUpdateAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _outbox.Verify(x => x.AddAsync(
            It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.RollbackTransactionAsync(
            CancellationToken.None), Times.Once);
    }

    private OrderExpirationProcessor CreateProcessor() => new(
        _orders.Object,
        _reservations.Object,
        _products.Object,
        _outbox.Object,
        _unitOfWork.Object,
        _cache.Object);
}
