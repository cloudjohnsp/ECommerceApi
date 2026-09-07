using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerce.Application.Users.Validators;

public sealed class GetUserByIdValidator : AbstractValidator<GetUserByIdQuery>
{ 
    public GetUserByIdValidator()
    {
        RuleFor(query => query.UserId)
            .NotEmpty().WithMessage("User ID is required.")
            .Must(id => id != Guid.Empty).WithMessage("User ID cannot be an empty GUID.");
    }
}
