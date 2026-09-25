using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Persistence.Contexts;
using ECommerce.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Persistence.Tests.Repositories;

public sealed class UserAuditRepositoryTests
{
    [Fact]
    public async Task GetByUserIdAsync_ReturnsNewestEntriesWithPagination()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var entries = Enumerable.Range(0, 4)
            .Select(_ => new UserAuditEntry(userId, null, UserAuditAction.ProfileUpdated, "{}"))
            .ToArray();
        context.UserAuditEntries.AddRange(entries);
        context.UserAuditEntries.Add(new UserAuditEntry(
            otherUserId, null, UserAuditAction.Created, "{}"));
        await context.SaveChangesAsync();
        var repository = new UserAuditRepository(context);

        var result = await repository.GetByUserIdAsync(userId, 2, 2);

        result.TotalCount.Should().Be(4);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(entry => entry.UserId == userId);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
