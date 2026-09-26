using ECommerce.Application.Users;
using ECommerce.Application.Users.Validators;
using FluentAssertions;

namespace ECommerce.Application.Tests.Users.Validators;

public sealed class ChangeUserPasswordValidatorTests
{
    private readonly ChangeUserPasswordValidator _validator = new();

    [Fact]
    public async Task Validate_WithValidDifferentPasswords_IsValid()
    {
        var command = new ChangeUserPasswordCommand(
            Guid.NewGuid(),
            "CurrentPassword1!",
            "NewPassword2!");

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithoutCurrentPassword_ReturnsRequiredError()
    {
        var command = new ChangeUserPasswordCommand(Guid.NewGuid(), null, "NewPassword2!");

        var result = await _validator.ValidateAsync(command);

        result.Errors.Select(error => error.ErrorMessage)
            .Should().Contain("Current password is required.");
    }

    [Fact]
    public async Task Validate_WithWeakNewPassword_ReturnsPolicyErrors()
    {
        var command = new ChangeUserPasswordCommand(Guid.NewGuid(), "CurrentPassword1!", "weak");

        var result = await _validator.ValidateAsync(command);

        result.Errors.Select(error => error.ErrorMessage).Should().Contain([
            "New password must be at least 8 characters long.",
            "New password must match the specified pattern."
        ]);
    }

    [Fact]
    public async Task Validate_WithSamePassword_ReturnsDifferentPasswordError()
    {
        var command = new ChangeUserPasswordCommand(
            Guid.NewGuid(),
            "SamePassword1!",
            "SamePassword1!");

        var result = await _validator.ValidateAsync(command);

        result.Errors.Select(error => error.ErrorMessage)
            .Should().Contain("New password must be different from the current password.");
    }
}
