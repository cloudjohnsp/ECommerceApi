using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Tests.Support;
using FluentAssertions;

namespace ECommerce.Domain.Tests.Entities;

public class UserTests
{
    [Fact]
    public void Create_WithValidData_ReturnsActiveCustomer()
    {
        var email = UserFactory.CreateEmail();

        var result = User.Create("  Jane  ", "  Doe  ", email, "hashed-password");

        result.IsSuccess.Should().BeTrue();
        var user = result.Value!;
        user.Id.Should().NotBeEmpty();
        user.FirstName.Should().Be("Jane");
        user.LastName.Should().Be("Doe");
        user.Email.Should().Be(email);
        user.PasswordHash.Should().Be("hashed-password");
        user.Role.Should().Be(UserRole.Customer);
        user.IsActive.Should().BeTrue();
        user.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        user.UpdatedAt.Should().BeNull();
        user.DeactivatedAt.Should().BeNull();
        user.RefreshTokens.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithAdministratorRole_SetsRole()
    {
        var result = User.Create("Jane", "Doe", UserFactory.CreateEmail(), "hashed-password", UserRole.Administrator);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Role.Should().Be(UserRole.Administrator);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingFirstName_ReturnsNameLengthError(string? firstName)
    {
        var result = User.Create(firstName!, "Doe", UserFactory.CreateEmail(), "hashed-password");

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("First name must contain between 1 and 100 characters.");
    }

    [Fact]
    public void Create_WithFirstNameLongerThan100Characters_ReturnsNameLengthError()
    {
        var result = User.Create(new string('A', 101), "Doe", UserFactory.CreateEmail(), "hashed-password");

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("First name must contain between 1 and 100 characters.");
    }

    [Fact]
    public void Create_WithNameAtMaxLength_Succeeds()
    {
        var result = User.Create(new string('A', 100), new string('B', 100), UserFactory.CreateEmail(), "hashed-password");

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("Jane123")]
    [InlineData("Jane.")]
    [InlineData("Jane@")]
    public void Create_WithInvalidNameCharacters_ReturnsCharacterError(string firstName)
    {
        var result = User.Create(firstName, "Doe", UserFactory.CreateEmail(), "hashed-password");

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("First name contains invalid characters.");
    }

    [Theory]
    [InlineData("José")]
    [InlineData("O'Brien")]
    [InlineData("Mary-Jane")]
    [InlineData("Anne Marie")]
    public void Create_WithAllowedNameCharacters_Succeeds(string firstName)
    {
        var result = User.Create(firstName, "Doe", UserFactory.CreateEmail(), "hashed-password");

        result.IsSuccess.Should().BeTrue();
        result.Value!.FirstName.Should().Be(firstName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingPasswordHash_ReturnsPasswordError(string? passwordHash)
    {
        var result = User.Create("Jane", "Doe", UserFactory.CreateEmail(), passwordHash!);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Password hash is required.");
    }

    [Fact]
    public void Create_WithNullEmail_ReturnsEmailRequiredError()
    {
        var result = User.Create("Jane", "Doe", null!, "hashed-password");

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("E-mail is required.");
    }

    [Fact]
    public void Create_WithMultipleInvalidFields_ReturnsAllErrors()
    {
        var result = User.Create("", "", null!, "");

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().BeEquivalentTo(
            "First name must contain between 1 and 100 characters.",
            "Last name must contain between 1 and 100 characters.",
            "E-mail is required.",
            "Password hash is required.");
    }

    [Fact]
    public void UpdateProfile_WithValidData_UpdatesNamesEmailAndTimestamp()
    {
        var user = UserFactory.Create();
        var newEmail = UserFactory.CreateEmail("john.smith@example.com");

        var result = user.UpdateProfile("  John  ", "  Smith  ", newEmail);

        result.IsSuccess.Should().BeTrue();
        user.FirstName.Should().Be("John");
        user.LastName.Should().Be("Smith");
        user.Email.Should().Be(newEmail);
        user.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdateProfile_WithInvalidName_DoesNotChangeUser()
    {
        var user = UserFactory.Create();
        var originalEmail = user.Email;

        var result = user.UpdateProfile("Jane123", "Doe", originalEmail);

        result.IsFailure.Should().BeTrue();
        user.FirstName.Should().Be("Jane");
        user.LastName.Should().Be("Doe");
        user.Email.Should().Be(originalEmail);
        user.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void ChangePassword_WithValidHash_UpdatesPasswordAndTimestamp()
    {
        var user = UserFactory.Create();

        var result = user.ChangePassword("new-hash");

        result.IsSuccess.Should().BeTrue();
        user.PasswordHash.Should().Be("new-hash");
        user.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ChangePassword_WithMissingHash_ReturnsErrorWithoutChangingPassword(string? passwordHash)
    {
        var user = UserFactory.Create();

        var result = user.ChangePassword(passwordHash!);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().Be("Password hash is required.");
        user.PasswordHash.Should().Be("hashed-password");
        user.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void ChangeRole_UpdatesRoleAndTimestamp()
    {
        var user = UserFactory.Create();

        var result = user.ChangeRole(UserRole.Administrator);

        result.IsSuccess.Should().BeTrue();
        user.Role.Should().Be(UserRole.Administrator);
        user.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Deactivate_WhenActive_MarksUserInactive()
    {
        var user = UserFactory.Create();

        var result = user.Deactivate();

        result.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeFalse();
        user.DeactivatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        user.UpdatedAt.Should().Be(user.DeactivatedAt);
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_DoesNotChangeTimestamps()
    {
        var user = UserFactory.Create();
        user.Deactivate();
        var deactivatedAt = user.DeactivatedAt;
        var updatedAt = user.UpdatedAt;

        var result = user.Deactivate();

        result.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeFalse();
        user.DeactivatedAt.Should().Be(deactivatedAt);
        user.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public void Activate_WhenInactive_ClearsDeactivation()
    {
        var user = UserFactory.Create();
        user.Deactivate();

        user.Activate();

        user.IsActive.Should().BeTrue();
        user.DeactivatedAt.Should().BeNull();
        user.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Activate_WhenAlreadyActive_DoesNotChangeUser()
    {
        var user = UserFactory.Create();

        user.Activate();

        user.IsActive.Should().BeTrue();
        user.DeactivatedAt.Should().BeNull();
        user.UpdatedAt.Should().BeNull();
    }
}
