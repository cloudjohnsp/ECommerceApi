using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using FluentAssertions;

namespace ECommerce.Domain.Tests.Entities;

public sealed class UserAuditEntryTests
{
    [Fact]
    public void Constructor_WithValidObjectJson_CreatesImmutableAuditEntry()
    {
        var userId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var entry = new UserAuditEntry(
            userId,
            actorId,
            UserAuditAction.RoleChanged,
            "{\"role\":{\"from\":\"Customer\",\"to\":\"Administrator\"}}");

        entry.UserId.Should().Be(userId);
        entry.ActorUserId.Should().Be(actorId);
        entry.Action.Should().Be(UserAuditAction.RoleChanged);
        entry.OccurredAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("[]")]
    public void Constructor_WithInvalidChanges_Throws(string changes)
    {
        var action = () => new UserAuditEntry(
            Guid.NewGuid(),
            null,
            UserAuditAction.ProfileUpdated,
            changes);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WhenChangesExceedLimit_Throws()
    {
        var changes = "{\"value\":\"" + new string('x', UserAuditEntry.MaximumChangesSizeBytes) + "\"}";

        var action = () => new UserAuditEntry(
            Guid.NewGuid(),
            null,
            UserAuditAction.ProfileUpdated,
            changes);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*maximum size*");
    }
}
