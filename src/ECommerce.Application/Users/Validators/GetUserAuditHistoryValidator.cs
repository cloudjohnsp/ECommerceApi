using FluentValidation;

namespace ECommerce.Application.Users.Validators;

public sealed class GetUserAuditHistoryValidator : AbstractValidator<GetUserAuditHistoryQuery>
{
    public GetUserAuditHistoryValidator()
    {
        RuleFor(query => query.UserId)
            .NotEmpty().WithMessage("User ID is required.");
        RuleFor(query => query.Page)
            .GreaterThan(0).WithMessage("Page must be greater than zero.");
        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
    }
}
