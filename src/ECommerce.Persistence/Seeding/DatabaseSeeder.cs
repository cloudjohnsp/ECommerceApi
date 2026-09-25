using ECommerce.Application.Abstractions.Security;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.ValueObjects;
using ECommerce.Persistence.Contexts;
using ECommerce.Persistence.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace ECommerce.Persistence.Seeding;

public sealed class DatabaseSeeder(
    AppDbContext dbContext,
    IPasswordHasher passwordHasher,
    IOptions<DatabaseSeedOptions> options)
{
    private const long AdvisoryLockId = 1_384_292_315;
    private readonly DatabaseSeedOptions _options = options.Value;

    private static readonly string[] CategoryNames =
    [
        "Books",
        "Electronics",
        "Home and Kitchen"
    ];

    private static readonly CatalogProduct[] Products =
    [
        new("Clean Architecture", "Software architecture reference book.", 149.90m, 20, "books"),
        new("Coffee Maker", "Compact programmable coffee maker.", 299.90m, 12, "home-and-kitchen"),
        new("Wireless Mouse", "Ergonomic wireless mouse.", 129.90m, 30, "electronics")
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return;

        await using var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
            if (dbContext.Database.IsNpgsql())
            {
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock({AdvisoryLockId})",
                    cancellationToken);
            }

            if (_options.SeedSampleCatalog)
                await SeedCatalogAsync(cancellationToken);

            if (_options.HasAdministratorCredentials)
                await SeedAdministratorAsync(cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task SeedCatalogAsync(CancellationToken cancellationToken)
    {
        var categories = await dbContext.Categories
            .IgnoreQueryFilters()
            .ToDictionaryAsync(category => category.Slug, cancellationToken);

        foreach (var name in CategoryNames)
        {
            var result = Category.Create(name);
            if (result.IsFailure)
                throw new InvalidOperationException(string.Join("; ", result.Errors));

            var category = result.Value!;
            if (categories.ContainsKey(category.Slug))
                continue;

            categories.Add(category.Slug, category);
            await dbContext.Categories.AddAsync(category, cancellationToken);
        }

        var existingNames = await dbContext.Products
            .IgnoreQueryFilters()
            .Select(product => product.Name)
            .ToHashSetAsync(cancellationToken);

        foreach (var sample in Products.Where(sample => !existingNames.Contains(sample.Name)))
        {
            var result = Product.Create(
                sample.Name,
                sample.Description,
                sample.Price,
                sample.Stock,
                categories[sample.CategorySlug].Id);
            if (result.IsFailure)
                throw new InvalidOperationException(string.Join("; ", result.Errors));

            await dbContext.Products.AddAsync(result.Value!, cancellationToken);
        }
    }

    private async Task SeedAdministratorAsync(CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(_options.AdministratorEmail);
        if (emailResult.IsFailure)
            throw new InvalidOperationException(string.Join("; ", emailResult.Errors));

        var email = emailResult.Value!;
        var existingUser = await dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(user => user.Email.Value == email.Value, cancellationToken);
        if (existingUser is not null)
        {
            if (existingUser.Role != UserRole.Administrator)
                throw new DatabaseSeedConflictException(
                    "The configured seed administrator e-mail already belongs to a non-administrator user.");
            return;
        }

        var passwordHash = passwordHasher.HashPassword(_options.AdministratorPassword);
        var userResult = User.Create(
            "System",
            "Administrator",
            email,
            passwordHash,
            UserRole.Administrator);
        if (userResult.IsFailure)
            throw new InvalidOperationException(string.Join("; ", userResult.Errors));

        var administrator = userResult.Value!;
        var confirmationResult = administrator.ConfirmEmail();
        if (confirmationResult.IsFailure)
            throw new InvalidOperationException(string.Join("; ", confirmationResult.Errors));

        await dbContext.Users.AddAsync(administrator, cancellationToken);
        await dbContext.UserAuditEntries.AddAsync(
            new UserAuditEntry(
                administrator.Id,
                null,
                UserAuditAction.Created,
                "{\"source\":\"database-seed\"}"),
            cancellationToken);
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
            return null;

        return await dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    private sealed record CatalogProduct(
        string Name,
        string Description,
        decimal Price,
        int Stock,
        string CategorySlug);
}
