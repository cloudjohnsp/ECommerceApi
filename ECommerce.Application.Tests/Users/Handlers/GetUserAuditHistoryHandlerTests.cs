using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Users;
using ECommerce.Application.Users.Handlers;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Tests.Support;
using ECommerce.Shared.Pagination;
using FluentAssertions;
using Moq;

namespace ECommerce.Application.Tests.Users.Handlers;

public sealed class GetUserAuditHistoryHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUserAuditRepository> _audits = new();

    [Fact]
    public async Task Handle_ForExistingUser_ReturnsPagedEntriesWithJsonChanges()
    {
        var user = UserFactory.Create();
        var entry = new UserAuditEntry(
            user.Id,
            user.Id,
            UserAuditAction.ProfileUpdated,
            "{\"firstName\":{\"from\":\"Jane\",\"to\":\"Janet\"}}");
        _users.Setup(repository => repository.GetByIdAsync(
                user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _audits.Setup(repository => repository.GetByUserIdAsync(
                user.Id, 2, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<UserAuditEntry>([entry], 2, 10, 11));
        var handler = new GetUserAuditHistoryHandler(_users.Object, _audits.Object);

        var result = await handler.Handle(
            new GetUserAuditHistoryQuery(user.Id, 2, 10),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(11);
        result.Value.Items.Should().ContainSingle();
        result.Value.Items.Single().Changes.GetProperty("firstName")
            .GetProperty("to").GetString().Should().Be("Janet");
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ReturnsNotFoundWithoutQueryingHistory()
    {
        var query = new GetUserAuditHistoryQuery(Guid.NewGuid());
        var handler = new GetUserAuditHistoryHandler(_users.Object, _audits.Object);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("User not found.");
        _audits.Verify(repository => repository.GetByUserIdAsync(
            It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
