using ECommerce.Application.Administration;
using ECommerce.Application.Administration.Validators;
using FluentAssertions;

namespace ECommerce.Application.Tests.Administration.Validators;

public sealed class GetAdminSalesReportValidatorTests
{
    private readonly GetAdminSalesReportValidator _validator = new();

    [Fact]
    public void Validate_WithValidPeriod_Succeeds()
    {
        var fromUtc = DateTimeOffset.UtcNow.AddDays(-30);

        var result = _validator.Validate(new GetAdminSalesReportQuery(fromUtc, fromUtc.AddDays(30), 10));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public void Validate_WithInvalidTopProducts_Fails(int topProducts)
    {
        var fromUtc = DateTimeOffset.UtcNow.AddDays(-1);

        var result = _validator.Validate(new GetAdminSalesReportQuery(fromUtc, fromUtc.AddDays(1), topProducts));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenPeriodIsReversed_Fails()
    {
        var fromUtc = DateTimeOffset.UtcNow;

        var result = _validator.Validate(new GetAdminSalesReportQuery(fromUtc, fromUtc.AddMinutes(-1)));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenPeriodExceedsLimit_Fails()
    {
        var fromUtc = DateTimeOffset.UtcNow.AddDays(-367);

        var result = _validator.Validate(new GetAdminSalesReportQuery(fromUtc, fromUtc.AddDays(367)));

        result.IsValid.Should().BeFalse();
    }
}
