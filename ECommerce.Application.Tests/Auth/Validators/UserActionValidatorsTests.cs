using ECommerce.Application.Auth;
using ECommerce.Application.Auth.Validators;
using FluentAssertions;

namespace ECommerce.Application.Tests.Auth.Validators;

public sealed class UserActionValidatorsTests
{
    [Fact]
    public async Task ForgotPassword_WithInvalidEmail_IsInvalid()
    {
        var result = await new ForgotPasswordValidator()
            .ValidateAsync(new ForgotPasswordCommand("invalid"));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("password")]
    [InlineData("Password1")]
    public async Task ResetPassword_WithWeakPassword_IsInvalid(string password)
    {
        var result = await new ResetPasswordValidator()
            .ValidateAsync(new ResetPasswordCommand("token", password));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "NewPassword");
    }

    [Fact]
    public async Task ResetPassword_WithExclamationMarkAsSpecialCharacter_IsValid()
    {
        var result = await new ResetPasswordValidator()
            .ValidateAsync(new ResetPasswordCommand("token", "NewPassword1!"));

        result.IsValid.Should().BeTrue();
    }
}
