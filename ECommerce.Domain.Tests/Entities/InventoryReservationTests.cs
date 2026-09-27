using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using FluentAssertions;

namespace ECommerce.Domain.Tests.Entities;

public sealed class InventoryReservationTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidData_CreatesActiveReservationWithDeadline()
    {
        var result = InventoryReservation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            3,
            CreatedAt,
            CreatedAt.AddMinutes(30));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(InventoryReservationStatus.Active);
        result.Value.Quantity.Should().Be(3);
        result.Value.ExpiresAt.Should().Be(CreatedAt.AddMinutes(30));
        result.Value.CompletedAt.Should().BeNull();
    }

    [Theory]
    [InlineData("order")]
    [InlineData("product")]
    [InlineData("inventory")]
    public void Create_WithMissingRelationship_ReturnsFailure(string missingRelationship)
    {
        var result = InventoryReservation.Create(
            missingRelationship == "order" ? Guid.Empty : Guid.NewGuid(),
            missingRelationship == "product" ? Guid.Empty : Guid.NewGuid(),
            missingRelationship == "inventory" ? Guid.Empty : Guid.NewGuid(),
            1,
            CreatedAt,
            CreatedAt.AddMinutes(30));

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_WithInvalidQuantityOrDeadline_ReturnsAllFailures()
    {
        var result = InventoryReservation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            0,
            CreatedAt,
            CreatedAt);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(InventoryReservationStatus.Consumed)]
    [InlineData(InventoryReservationStatus.Released)]
    [InlineData(InventoryReservationStatus.Expired)]
    public void Complete_FromActive_TransitionsAndIsIdempotent(
        InventoryReservationStatus targetStatus)
    {
        var reservation = CreateReservation();
        var completedAt = CreatedAt.AddHours(1);

        var first = Complete(reservation, targetStatus, completedAt);
        var second = Complete(reservation, targetStatus, completedAt.AddMinutes(1));

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        reservation.Status.Should().Be(targetStatus);
        reservation.CompletedAt.Should().Be(completedAt);
    }

    [Fact]
    public void Complete_AfterDifferentTerminalState_ReturnsFailureAndPreservesState()
    {
        var reservation = CreateReservation();
        reservation.Release(CreatedAt.AddMinutes(5));

        var result = reservation.Consume(CreatedAt.AddMinutes(10));

        result.IsFailure.Should().BeTrue();
        reservation.Status.Should().Be(InventoryReservationStatus.Released);
    }

    [Fact]
    public void Expire_BeforeDeadline_ReturnsFailureAndPreservesActiveState()
    {
        var reservation = CreateReservation();

        var result = reservation.Expire(CreatedAt.AddMinutes(29));

        result.IsFailure.Should().BeTrue();
        reservation.Status.Should().Be(InventoryReservationStatus.Active);
        reservation.CompletedAt.Should().BeNull();
    }

    private static InventoryReservation CreateReservation() =>
        InventoryReservation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            2,
            CreatedAt,
            CreatedAt.AddMinutes(30)).Value!;

    private static ECommerce.Shared.Results.Result Complete(
        InventoryReservation reservation,
        InventoryReservationStatus status,
        DateTimeOffset completedAt) => status switch
        {
            InventoryReservationStatus.Consumed => reservation.Consume(completedAt),
            InventoryReservationStatus.Released => reservation.Release(completedAt),
            InventoryReservationStatus.Expired => reservation.Expire(completedAt),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
}
