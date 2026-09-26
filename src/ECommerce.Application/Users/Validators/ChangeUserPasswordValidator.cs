using ECommerce.Application.Users;
using FluentValidation;
using ECommerce.Application.Security;

namespace ECommerce.Application.Users.Validators;

public sealed class ChangeUserPasswordValidator : AbstractValidator<ChangeUserPasswordCommand>
{
    public ChangeUserPasswordValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty().WithMessage("User ID is required.");
        RuleFor(command => command.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.");
        RuleFor(command => command.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(PasswordPolicy.MinimumLength)
            .WithMessage("New password must be at least 8 characters long.")
            .Matches(PasswordPolicy.Pattern)
            .WithMessage("New password must match the specified pattern.");
        RuleFor(command => command.NewPassword)
            .NotEqual(command => command.CurrentPassword)
            .When(command =>
                !string.IsNullOrEmpty(command.CurrentPassword) &&
                !string.IsNullOrEmpty(command.NewPassword))
            .WithMessage("New password must be different from the current password.");
    }
}
