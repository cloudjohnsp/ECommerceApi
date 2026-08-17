using ECommerce.Application.Auth.Commands;
using FluentValidation;

namespace ECommerce.Application.Auth.Validators;

public sealed class LogoutValidator : AbstractValidator<LogoutCommand>
{
    public LogoutValidator()
    {
        RuleFor(command => command.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}
