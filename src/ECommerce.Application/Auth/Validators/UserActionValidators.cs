using FluentValidation;

namespace ECommerce.Application.Auth.Validators;

public sealed class ConfirmEmailValidator : AbstractValidator<ConfirmEmailCommand>
{
    public ConfirmEmailValidator() => RuleFor(command => command.Token).NotEmpty().MaximumLength(512);
}

public sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordValidator() => RuleFor(command => command.Email).NotEmpty().EmailAddress();
}

public sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(command => command.Token).NotEmpty().MaximumLength(512);
        RuleFor(command => command.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("^(?=.*[0-9])(?=.*[a-z])(?=.*[A-Z])(?=.*[@#$%^&+=])(?=\\S+$).{8,20}$")
            .WithMessage("Password must match the specified pattern.");
    }
}
