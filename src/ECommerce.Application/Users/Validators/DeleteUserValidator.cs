using ECommerce.Application.Users.Commands;
using FluentValidation;

namespace ECommerce.Application.Users.Validators;

public sealed class DeleteUserValidator : AbstractValidator<DeleteUserCommand>
{
    public DeleteUserValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty().WithMessage("User ID is required.");
    }
}
