using ECommerce.Application.Categories.Dtos;

namespace ECommerce.Application.Abstractions.Caching;

public interface ICategoryCache
{
    Task<CategoryDto?> GetAsync(Guid categoryId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<CategoryDto>?> GetAllAsync(CancellationToken cancellationToken = default);
    Task SetAsync(CategoryDto category, CancellationToken cancellationToken = default);
    Task SetAllAsync(
        IReadOnlyCollection<CategoryDto> categories,
        CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid categoryId, CancellationToken cancellationToken = default);
    Task RemoveAllAsync(CancellationToken cancellationToken = default);
}
