using ECommerce.Application.Abstractions.Caching;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Categories.Dtos;
using ECommerce.Domain.Entities;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Categories.Handlers;

public sealed class CreateCategoryHandler(
    ICategoryRepository repository,
    IUnitOfWork unitOfWork,
    ICategoryCache categoryCache) : IRequestHandler<CreateCategoryCommand, Result<CategoryDto>>
{
    public async Task<Result<CategoryDto>> Handle(
        CreateCategoryCommand request,
        CancellationToken cancellationToken)
    {
        var categoryResult = Category.Create(request.Name);
        if (categoryResult.IsFailure)
            return Result<CategoryDto>.Failure([.. categoryResult.Errors]);

        var category = categoryResult.Value!;
        var transactionCommitted = false;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await repository.AcquireSlugLockAsync(category.Slug, cancellationToken);
            if (await repository.ExistsBySlugAsync(
                    category.Slug,
                    cancellationToken: cancellationToken))
            {
                return Result<CategoryDto>.Failure("A category with this name already exists.");
            }

            await repository.AddAsync(category, cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            transactionCommitted = true;
            var categoryDto = category.ToDto();
            await categoryCache.SetAsync(categoryDto, CancellationToken.None);
            await categoryCache.RemoveAllAsync(CancellationToken.None);
            return Result<CategoryDto>.Success(categoryDto);
        }
        finally
        {
            if (!transactionCommitted)
                await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
        }
    }
}

public sealed class UpdateCategoryHandler(
    ICategoryRepository repository,
    IUnitOfWork unitOfWork,
    ICategoryCache categoryCache) : IRequestHandler<UpdateCategoryCommand, Result<CategoryDto>>
{
    public async Task<Result<CategoryDto>> Handle(
        UpdateCategoryCommand request,
        CancellationToken cancellationToken)
    {
        var transactionCommitted = false;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var category = await repository.GetByIdForUpdateAsync(
                request.CategoryId,
                cancellationToken);
            if (category is null)
                return Result<CategoryDto>.Failure("Category not found.");

            var previousSlug = category.Slug;
            var updateResult = category.Update(request.Name);
            if (updateResult.IsFailure)
                return Result<CategoryDto>.Failure([.. updateResult.Errors]);
            if (category.Slug != previousSlug)
            {
                await repository.AcquireSlugLockAsync(category.Slug, cancellationToken);
                if (await repository.ExistsBySlugAsync(
                        category.Slug,
                        category.Id,
                        cancellationToken))
                {
                    return Result<CategoryDto>.Failure("A category with this name already exists.");
                }
            }

            repository.Update(category);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            transactionCommitted = true;
            var categoryDto = category.ToDto();
            await categoryCache.SetAsync(categoryDto, CancellationToken.None);
            await categoryCache.RemoveAllAsync(CancellationToken.None);
            return Result<CategoryDto>.Success(categoryDto);
        }
        finally
        {
            if (!transactionCommitted)
                await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
        }
    }
}

public sealed class DeleteCategoryHandler(
    ICategoryRepository repository,
    IUnitOfWork unitOfWork,
    ICategoryCache categoryCache) : IRequestHandler<DeleteCategoryCommand, Result>
{
    public async Task<Result> Handle(
        DeleteCategoryCommand request,
        CancellationToken cancellationToken)
    {
        var transactionCommitted = false;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var category = await repository.GetByIdForUpdateAsync(
                request.CategoryId,
                cancellationToken);
            if (category is null)
                return Result.Failure("Category not found.");

            var result = category.Deactivate();
            if (result.IsFailure)
                return result;

            repository.Update(category);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            transactionCommitted = true;
            await categoryCache.RemoveAsync(category.Id, CancellationToken.None);
            await categoryCache.RemoveAllAsync(CancellationToken.None);
            return Result.Success();
        }
        finally
        {
            if (!transactionCommitted)
                await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
        }
    }
}

public sealed class GetCategoryByIdHandler(
    ICategoryRepository repository,
    ICategoryCache categoryCache)
    : IRequestHandler<GetCategoryByIdQuery, Result<CategoryDto>>
{
    public async Task<Result<CategoryDto>> Handle(
        GetCategoryByIdQuery request,
        CancellationToken cancellationToken)
    {
        var cachedCategory = await categoryCache.GetAsync(request.CategoryId, cancellationToken);
        if (cachedCategory is not null)
            return Result<CategoryDto>.Success(cachedCategory);

        var category = await repository.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category is null)
            return Result<CategoryDto>.Failure("Category not found.");

        var categoryDto = category.ToDto();
        await categoryCache.SetAsync(categoryDto, cancellationToken);
        return Result<CategoryDto>.Success(categoryDto);
    }
}

public sealed class GetCategoriesHandler(
    ICategoryRepository repository,
    ICategoryCache categoryCache)
    : IRequestHandler<GetCategoriesQuery, Result<IReadOnlyCollection<CategoryDto>>>
{
    public async Task<Result<IReadOnlyCollection<CategoryDto>>> Handle(
        GetCategoriesQuery request,
        CancellationToken cancellationToken)
    {
        var cachedCategories = await categoryCache.GetAllAsync(cancellationToken);
        if (cachedCategories is not null)
            return Result<IReadOnlyCollection<CategoryDto>>.Success(cachedCategories);

        var categories = await repository.GetAllAsync(cancellationToken);
        IReadOnlyCollection<CategoryDto> categoryDtos =
            categories.Select(category => category.ToDto()).ToArray();
        await categoryCache.SetAllAsync(categoryDtos, cancellationToken);
        return Result<IReadOnlyCollection<CategoryDto>>.Success(categoryDtos);
    }
}
