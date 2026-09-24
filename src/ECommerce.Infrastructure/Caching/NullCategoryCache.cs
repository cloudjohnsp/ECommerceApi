using ECommerce.Application.Abstractions.Caching;
using ECommerce.Application.Categories.Dtos;

namespace ECommerce.Infrastructure.Caching;

public sealed class NullCategoryCache : ICategoryCache
{
    public Task<CategoryDto?> GetAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        Task.FromResult<CategoryDto?>(null);

    public Task<IReadOnlyCollection<CategoryDto>?> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyCollection<CategoryDto>?>(null);

    public Task SetAsync(CategoryDto category, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SetAllAsync(
        IReadOnlyCollection<CategoryDto> categories,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RemoveAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RemoveAllAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
