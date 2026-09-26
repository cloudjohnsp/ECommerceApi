using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;
using ECommerce.Application.Security;

namespace ECommerce.Application.Users.Validators
{
    public sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
    {
        public CreateUserValidator()
        {
            RuleFor(user => user.FirstName)
                .NotEmpty().WithMessage("First name is required.")
                .MaximumLength(100).WithMessage("First name cannot exceed 100 characters.");
            RuleFor(user => user.LastName)
                .NotEmpty().WithMessage("Last name is required.")
                .MaximumLength(100).WithMessage("Last name cannot exceed 100 characters.");
            RuleFor(user => user.Email).NotEmpty().EmailAddress().WithMessage("Email is required.")
                .WithMessage("Invalid email format.");
            RuleFor(user => user.Password)
                .NotEmpty().WithMessage("Password is required.")
                .MinimumLength(PasswordPolicy.MinimumLength)
                .WithMessage("Password must be at least 8 characters long.")
                .Matches(PasswordPolicy.Pattern)
                .WithMessage("Password must match the specified pattern.");
            RuleFor(user => user.Role)
                .IsInEnum().WithMessage("Invalid role.");
        }
    }
}
