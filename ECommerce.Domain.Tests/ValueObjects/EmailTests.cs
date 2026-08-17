using ECommerce.Domain.ValueObjects;
using FluentAssertions;

namespace ECommerce.Domain.Tests.ValueObjects;

public class EmailTests
{
    [Theory]
    [InlineData("jane.doe@example.com")]
    [InlineData("  Jane.Doe@Example.COM  ")]
    public void Create_WithValidValue_NormalizesToLowercaseTrimmedAddress(string value)
    {
        var result = Email.Create(value);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Value.Should().Be("jane.doe@example.com");
        result.Value.ToString().Should().Be("jane.doe@example.com");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingValue_ReturnsRequiredError(string? value)
    {
        var result = Email.Create(value!);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().Be("E-mail is required.");
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("user@")]
    [InlineData("@example.com")]
    [InlineData("user@example.com extra")]
    public void Create_WithInvalidValue_ReturnsInvalidError(string value)
    {
        var result = Email.Create(value);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().Be("E-mail is invalid.");
    }

    [Fact]
    public void Emails_WithSameNormalizedValue_AreEqual()
    {
        var left = Email.Create("Jane.Doe@Example.com").Value!;
        var right = Email.Create(" jane.doe@example.com ").Value!;

        left.Should().Be(right);
    }
}
