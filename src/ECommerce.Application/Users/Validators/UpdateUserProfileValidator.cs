using ECommerce.Application.Users;
using FluentValidation;

namespace ECommerce.Application.Users.Validators;

public sealed class UpdateUserProfileValidator : AbstractValidator<UpdateUserProfileCommand>
{
    public UpdateUserProfileValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty().WithMessage("User ID is required.");
        RuleFor(command => command.FirstName)
            .MaximumLength(100).WithMessage("First name cannot exceed 100 characters.")
            .When(command => command.FirstName is not null);
        RuleFor(command => command.LastName)
            .MaximumLength(100).WithMessage("Last name cannot exceed 100 characters.")
            .When(command => command.LastName is not null);
        RuleFor(command => command.Email)
            .EmailAddress().WithMessage("Invalid email format.")
            .When(command => !string.IsNullOrWhiteSpace(command.Email));
    }
}
