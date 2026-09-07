using ECommerce.Application.Users;
using ECommerce.Domain.Enums;
using FluentValidation;

namespace ECommerce.Application.Users.Validators;

public sealed class ChangeUserRoleValidator : AbstractValidator<ChangeUserRoleCommand>
{
    public ChangeUserRoleValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty().WithMessage("User ID is required.");
        RuleFor(command => command.Role)
            .IsInEnum().WithMessage("Invalid role.");
    }
}
