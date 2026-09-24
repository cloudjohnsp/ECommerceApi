using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Categories.Dtos;
using ECommerce.Domain.Entities;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Categories.Handlers;

public sealed class CreateCategoryHandler(
    ICategoryRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateCategoryCommand, Result<CategoryDto>>
{
    public async Task<Result<CategoryDto>> Handle(
        CreateCategoryCommand request,
        CancellationToken cancellationToken)
    {
        var categoryResult = Category.Create(request.Name);
        if (categoryResult.IsFailure)
            return Result<CategoryDto>.Failure([.. categoryResult.Errors]);

        var category = categoryResult.Value!;
        if (await repository.ExistsBySlugAsync(category.Slug, cancellationToken: cancellationToken))
            return Result<CategoryDto>.Failure("A category with this name already exists.");

        await repository.AddAsync(category, cancellationToken);
        await unitOfWork.Commit(cancellationToken);
        return Result<CategoryDto>.Success(category.ToDto());
    }
}

public sealed class UpdateCategoryHandler(
    ICategoryRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateCategoryCommand, Result<CategoryDto>>
{
    public async Task<Result<CategoryDto>> Handle(
        UpdateCategoryCommand request,
        CancellationToken cancellationToken)
    {
        var category = await repository.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category is null)
            return Result<CategoryDto>.Failure("Category not found.");

        var updateResult = category.Update(request.Name);
        if (updateResult.IsFailure)
            return Result<CategoryDto>.Failure([.. updateResult.Errors]);
        if (await repository.ExistsBySlugAsync(
                category.Slug,
                category.Id,
                cancellationToken))
        {
            return Result<CategoryDto>.Failure("A category with this name already exists.");
        }

        repository.Update(category);
        await unitOfWork.Commit(cancellationToken);
        return Result<CategoryDto>.Success(category.ToDto());
    }
}

public sealed class DeleteCategoryHandler(
    ICategoryRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteCategoryCommand, Result>
{
    public async Task<Result> Handle(
        DeleteCategoryCommand request,
        CancellationToken cancellationToken)
    {
        var category = await repository.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category is null)
            return Result.Failure("Category not found.");

        var result = category.Deactivate();
        if (result.IsFailure)
            return result;

        repository.Update(category);
        await unitOfWork.Commit(cancellationToken);
        return Result.Success();
    }
}

public sealed class GetCategoryByIdHandler(ICategoryRepository repository)
    : IRequestHandler<GetCategoryByIdQuery, Result<CategoryDto>>
{
    public async Task<Result<CategoryDto>> Handle(
        GetCategoryByIdQuery request,
        CancellationToken cancellationToken)
    {
        var category = await repository.GetByIdAsync(request.CategoryId, cancellationToken);
        return category is null
            ? Result<CategoryDto>.Failure("Category not found.")
            : Result<CategoryDto>.Success(category.ToDto());
    }
}

public sealed class GetCategoriesHandler(ICategoryRepository repository)
    : IRequestHandler<GetCategoriesQuery, Result<IReadOnlyCollection<CategoryDto>>>
{
    public async Task<Result<IReadOnlyCollection<CategoryDto>>> Handle(
        GetCategoriesQuery request,
        CancellationToken cancellationToken)
    {
        var categories = await repository.GetAllAsync(cancellationToken);
        return Result<IReadOnlyCollection<CategoryDto>>.Success(
            categories.Select(category => category.ToDto()).ToArray());
    }
}
