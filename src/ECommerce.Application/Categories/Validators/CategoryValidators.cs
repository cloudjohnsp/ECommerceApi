using FluentValidation;

namespace ECommerce.Application.Categories.Validators;

public sealed class CreateCategoryValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryValidator() => RuleFor(command => command.Name).NotEmpty().MaximumLength(100);
}

public sealed class UpdateCategoryValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryValidator()
    {
        RuleFor(command => command.CategoryId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(100);
    }
}

public sealed class DeleteCategoryValidator : AbstractValidator<DeleteCategoryCommand>
{
    public DeleteCategoryValidator() => RuleFor(command => command.CategoryId).NotEmpty();
}

public sealed class GetCategoryByIdValidator : AbstractValidator<GetCategoryByIdQuery>
{
    public GetCategoryByIdValidator() => RuleFor(query => query.CategoryId).NotEmpty();
}
