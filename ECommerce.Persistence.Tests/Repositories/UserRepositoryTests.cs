using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.ValueObjects;
using ECommerce.Persistence.Contexts;
using ECommerce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Persistence.Tests.Repositories;

public sealed class UserRepositoryTests
{
    [Fact]
    public async Task GetByIdAsync_WithExistingUser_ReturnsUser()
    {
        await using var context = CreateContext();
        var user = CreateUser();
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var repository = new UserRepository(context);

        var result = await repository.GetByIdAsync(user.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
        result.Email.Value.Should().Be(user.Email.Value);
    }

    [Fact]
    public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
    {
        await using var context = CreateContext();
        var repository = new UserRepository(context);

        var result = await repository.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByEmailAsync_NormalizesEmailBeforeQuerying()
    {
        await using var context = CreateContext();
        var user = CreateUser(email: "jane.doe@example.com");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var repository = new UserRepository(context);

        var result = await repository.GetByEmailAsync("  JANE.DOE@EXAMPLE.COM  ");

        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task ExistsByEmailAsync_ReturnsWhetherEmailExists()
    {
        await using var context = CreateContext();
        var user = CreateUser(email: "jane.doe@example.com");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var repository = new UserRepository(context);

        var existing = await repository.ExistsByEmailAsync("JANE.DOE@EXAMPLE.COM");
        var missing = await repository.ExistsByEmailAsync("missing@example.com");

        existing.Should().BeTrue();
        missing.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsByEmailAsync_WithExcludedUser_DoesNotMatchExcludedUser()
    {
        await using var context = CreateContext();
        var user = CreateUser(email: "jane.doe@example.com");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var repository = new UserRepository(context);

        var result = await repository.ExistsByEmailAsync(user.Email.Value, user.Id);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsByEmailAsync_WithExcludedUser_ReturnsTrueForAnotherUser()
    {
        await using var context = CreateContext();
        var excludedUser = CreateUser(email: "jane.doe@example.com");
        var otherUser = CreateUser(email: "john.doe@example.com");
        context.Users.AddRange(excludedUser, otherUser);
        await context.SaveChangesAsync();
        var repository = new UserRepository(context);

        var result = await repository.ExistsByEmailAsync(otherUser.Email.Value, excludedUser.Id);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task AddAsync_AddsUserToDatabase()
    {
        await using var context = CreateContext();
        var repository = new UserRepository(context);
        var user = CreateUser();

        await repository.AddAsync(user);
        await context.SaveChangesAsync();

        var persistedUser = await context.Users.SingleAsync(entity => entity.Id == user.Id);
        persistedUser.Email.Value.Should().Be(user.Email.Value);
        persistedUser.FirstName.Should().Be(user.FirstName);
    }

    [Fact]
    public async Task UpdateAsync_PersistsUserChanges()
    {
        await using var context = CreateContext();
        var user = CreateUser();
        context.Users.Add(user);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var repository = new UserRepository(context);
        var userToUpdate = await repository.GetByIdAsync(user.Id);
        userToUpdate.Should().NotBeNull();

        var newEmail = CreateEmail("john.smith@example.com");
        userToUpdate!.UpdateProfile("John", "Smith", newEmail).IsSuccess.Should().BeTrue();
        await repository.UpdateAsync(userToUpdate);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var persistedUser = await context.Users.AsNoTracking().SingleAsync(entity => entity.Id == user.Id);
        persistedUser.FirstName.Should().Be("John");
        persistedUser.LastName.Should().Be("Smith");
        persistedUser.Email.Value.Should().Be("john.smith@example.com");
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static User CreateUser(string email = "jane.doe@example.com")
    {
        var result = User.Create("Jane", "Doe", CreateEmail(email), "hashed-password", UserRole.Customer);
        result.IsSuccess.Should().BeTrue();
        return result.Value!;
    }

    private static Email CreateEmail(string value)
    {
        var result = Email.Create(value);
        result.IsSuccess.Should().BeTrue();
        return result.Value!;
    }
}
