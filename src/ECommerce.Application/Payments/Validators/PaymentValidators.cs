using FluentValidation;

namespace ECommerce.Application.Payments.Validators;

public sealed class CreatePaymentValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Currency).NotEmpty().Length(3).Matches("^[A-Za-z]{3}$");
    }
}

public sealed class GetPaymentByOrderIdValidator : AbstractValidator<GetPaymentByOrderIdQuery>
{
    public GetPaymentByOrderIdValidator() => RuleFor(x => x.OrderId).NotEmpty();
}

public sealed class ProcessPaymentWebhookValidator : AbstractValidator<ProcessPaymentWebhookCommand>
{
    public ProcessPaymentWebhookValidator()
    {
        RuleFor(x => x.Payload).NotEmpty();
    }
}
