using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Persistence.Repositories;

public sealed class CategoryRepository(AppDbContext dbContext) : ICategoryRepository
{
    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Categories.FirstOrDefaultAsync(category => category.Id == id, cancellationToken);

    public Task<Category?> GetByIdForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default) => dbContext.Database.IsRelational()
            ? dbContext.Categories
                .FromSqlInterpolated($"SELECT * FROM categories WHERE \"Id\" = {id} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
            : dbContext.Categories.SingleOrDefaultAsync(
                category => category.Id == id,
                cancellationToken);

    public async Task<IReadOnlyCollection<Category>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        await dbContext.Categories.AsNoTracking()
            .OrderBy(category => category.Name)
            .ToArrayAsync(cancellationToken);

    public async Task AcquireSlugLockAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        if (!dbContext.Database.IsRelational())
            return;

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var lockKey = $"category:{normalizedSlug}";
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
            cancellationToken);
    }

    public Task<bool> ExistsBySlugAsync(
        string slug,
        Guid? excludedId = null,
        CancellationToken cancellationToken = default) =>
        dbContext.Categories.IgnoreQueryFilters().AnyAsync(
            category => category.Slug == slug &&
                        (!excludedId.HasValue || category.Id != excludedId.Value),
            cancellationToken);

    public async Task AddAsync(Category category, CancellationToken cancellationToken = default) =>
        await dbContext.Categories.AddAsync(category, cancellationToken);

    public void Update(Category category) => dbContext.Categories.Update(category);
}
