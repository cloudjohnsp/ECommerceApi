using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.ValueObjects;
using ECommerce.Persistence.Contexts;
using ECommerce.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Persistence.Tests.Repositories;

public sealed class UserActionTokenRepositoryTests
{
    private const string FirstHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string SecondHash = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    [Fact]
    public async Task ConsumeActiveForUserAsync_ConsumesOnlyMatchingActiveTokens()
    {
        await using var context = CreateContext();
        var user = User.Create(
            "Jane",
            "Doe",
            Email.Create("jane@example.com").Value!,
            "hashed-password").Value!;
        var now = DateTimeOffset.UtcNow;
        var confirmation = UserActionToken.Create(
            user.Id, FirstHash, UserActionTokenType.EmailConfirmation, now.AddHours(1)).Value!;
        var passwordReset = UserActionToken.Create(
            user.Id, SecondHash, UserActionTokenType.PasswordReset, now.AddHours(1)).Value!;
        context.Users.Add(user);
        context.UserActionTokens.AddRange(confirmation, passwordReset);
        await context.SaveChangesAsync();
        var repository = new UserActionTokenRepository(context);

        await repository.ConsumeActiveForUserAsync(
            user.Id, UserActionTokenType.EmailConfirmation, now);
        await context.SaveChangesAsync();

        confirmation.ConsumedAt.Should().Be(now);
        passwordReset.ConsumedAt.Should().BeNull();
    }

    [Fact]
    public async Task GetByHashForUpdateAsync_WithInMemoryProvider_ReturnsTrackedToken()
    {
        await using var context = CreateContext();
        var user = User.Create(
            "Jane",
            "Doe",
            Email.Create("lock-token@example.com").Value!,
            "hashed-password").Value!;
        var token = UserActionToken.Create(
            user.Id,
            FirstHash,
            UserActionTokenType.PasswordReset,
            DateTimeOffset.UtcNow.AddHours(1)).Value!;
        context.AddRange(user, token);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var repository = new UserActionTokenRepository(context);

        var result = await repository.GetByHashForUpdateAsync(
            FirstHash,
            UserActionTokenType.PasswordReset);

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
