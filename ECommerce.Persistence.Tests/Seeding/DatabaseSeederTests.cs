using ECommerce.Application.Abstractions.Security;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.ValueObjects;
using ECommerce.Persistence.Contexts;
using ECommerce.Persistence.Options;
using ECommerce.Persistence.Seeding;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ECommerce.Persistence.Tests.Seeding;

public sealed class DatabaseSeederTests
{
    [Fact]
    public async Task SeedAsync_WithCatalogAndAdministrator_IsIdempotent()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DatabaseSeedOptions
        {
            Enabled = true,
            SeedSampleCatalog = true,
            AdministratorEmail = "admin@example.com",
            AdministratorPassword = "StrongPassword1!"
        };

        await using (var firstContext = CreateContext(databaseName))
            await CreateSeeder(firstContext, options).SeedAsync();
        await using (var secondContext = CreateContext(databaseName))
            await CreateSeeder(secondContext, options).SeedAsync();

        await using var assertionContext = CreateContext(databaseName);
        var administrator = await assertionContext.Users.SingleAsync();
        var products = await assertionContext.Products.Include(product => product.Inventory).ToArrayAsync();

        (await assertionContext.Categories.CountAsync()).Should().Be(3);
        products.Should().HaveCount(3);
        products.Should().OnlyContain(product => product.AvailableStock > 0);
        administrator.Email.Value.Should().Be("admin@example.com");
        administrator.Role.Should().Be(UserRole.Administrator);
        administrator.IsEmailConfirmed.Should().BeTrue();
        administrator.PasswordHash.Should().Be("hashed:StrongPassword1!");
        (await assertionContext.UserAuditEntries.CountAsync(entry =>
            entry.UserId == administrator.Id && entry.Action == UserAuditAction.Created)).Should().Be(1);
    }

    [Fact]
    public async Task SeedAsync_WhenAdministratorEmailBelongsToCustomer_RejectsPrivilegeEscalation()
    {
        await using var context = CreateContext(Guid.NewGuid().ToString());
        var email = Email.Create("admin@example.com").Value!;
        var customer = User.Create("Existing", "Customer", email, "hash").Value!;
        context.Users.Add(customer);
        await context.SaveChangesAsync();
        var seeder = CreateSeeder(context, new DatabaseSeedOptions
        {
            Enabled = true,
            SeedSampleCatalog = false,
            AdministratorEmail = email.Value,
            AdministratorPassword = "StrongPassword1!"
        });

        var action = () => seeder.SeedAsync();

        await action.Should().ThrowAsync<DatabaseSeedConflictException>()
            .WithMessage("*non-administrator user*");
        customer.Role.Should().Be(UserRole.Customer);
    }

    [Fact]
    public async Task SeedAsync_WhenDisabled_DoesNotWriteData()
    {
        await using var context = CreateContext(Guid.NewGuid().ToString());
        var seeder = CreateSeeder(context, new DatabaseSeedOptions
        {
            Enabled = false,
            AdministratorEmail = "admin@example.com",
            AdministratorPassword = "StrongPassword1!"
        });

        await seeder.SeedAsync();

        (await context.Users.CountAsync()).Should().Be(0);
        (await context.Categories.CountAsync()).Should().Be(0);
    }

    private static DatabaseSeeder CreateSeeder(AppDbContext context, DatabaseSeedOptions options) =>
        new(context, new FakePasswordHasher(), Microsoft.Extensions.Options.Options.Create(options));

    private static AppDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new AppDbContext(options);
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password) => $"hashed:{password}";

        public bool VerifyPassword(string password, string hashedPassword) =>
            hashedPassword == HashPassword(password);
    }
}
