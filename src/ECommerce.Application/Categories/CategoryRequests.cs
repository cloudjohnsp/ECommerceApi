using ECommerce.Application.Categories.Dtos;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Categories;

public sealed record CreateCategoryCommand(string Name) : IRequest<Result<CategoryDto>>;
public sealed record UpdateCategoryCommand(Guid CategoryId, string Name) : IRequest<Result<CategoryDto>>;
public sealed record DeleteCategoryCommand(Guid CategoryId) : IRequest<Result>;
public sealed record GetCategoryByIdQuery(Guid CategoryId) : IRequest<Result<CategoryDto>>;
public sealed record GetCategoriesQuery : IRequest<Result<IReadOnlyCollection<CategoryDto>>>;
