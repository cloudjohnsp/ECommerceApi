using ECommerce.Domain.Entities;
using ECommerce.Persistence.Contexts;
using ECommerce.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Persistence.Tests.Repositories;

public sealed class RefreshTokenRepositoryTests
{
    [Fact]
    public async Task GetByTokenHashForUpdateAsync_WithInMemoryProvider_ReturnsTrackedToken()
    {
        await using var context = CreateContext();
        var token = RefreshToken.Create(
            Guid.NewGuid(),
            "token-hash",
            DateTimeOffset.UtcNow.AddDays(1));
        await context.RefreshTokens.AddAsync(token);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var repository = new RefreshTokenRepository(context);

        var result = await repository.GetByTokenHashForUpdateAsync("token-hash");

        result.Should().NotBeNull();
        context.Entry(result!).State.Should().Be(EntityState.Unchanged);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
