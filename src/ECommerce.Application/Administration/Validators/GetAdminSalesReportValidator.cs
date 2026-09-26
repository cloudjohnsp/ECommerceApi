using FluentValidation;

namespace ECommerce.Application.Administration.Validators;

public sealed class GetAdminSalesReportValidator : AbstractValidator<GetAdminSalesReportQuery>
{
    public GetAdminSalesReportValidator()
    {
        RuleFor(query => query.FromUtc)
            .NotEqual(default(DateTimeOffset));
        RuleFor(query => query.ToUtc)
            .NotEqual(default(DateTimeOffset))
            .GreaterThan(query => query.FromUtc)
            .WithMessage("ToUtc must be greater than FromUtc.");
        RuleFor(query => query)
            .Must(query => query.ToUtc - query.FromUtc <= TimeSpan.FromDays(366))
            .When(query => query.FromUtc != default && query.ToUtc > query.FromUtc)
            .WithMessage("The report period cannot exceed 366 days.");
        RuleFor(query => query.TopProducts)
            .InclusiveBetween(1, 50);
    }
}
