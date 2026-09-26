using ECommerce.Application.Orders;
using ECommerce.Application.Orders.Validators;
using FluentAssertions;

namespace ECommerce.Application.Tests.Orders.Validators;

public sealed class SearchOrdersValidatorTests
{
    private readonly SearchOrdersValidator _validator = new();

    [Fact]
    public void Validate_WithSupportedFilters_Succeeds()
    {
        var fromUtc = DateTimeOffset.UtcNow.AddDays(-7);

        var result = _validator.Validate(new SearchOrdersQuery(
            CreatedFromUtc: fromUtc,
            CreatedToUtc: fromUtc.AddDays(7),
            SortBy: "total",
            PageSize: 100));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("unknown", 1, 20)]
    [InlineData("createdAt", 0, 20)]
    [InlineData("createdAt", 1, 101)]
    public void Validate_WithInvalidSortingOrPagination_Fails(string sortBy, int page, int pageSize)
    {
        var result = _validator.Validate(new SearchOrdersQuery(
            SortBy: sortBy,
            Page: page,
            PageSize: pageSize));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithReversedPeriod_Fails()
    {
        var fromUtc = DateTimeOffset.UtcNow;

        var result = _validator.Validate(new SearchOrdersQuery(
            CreatedFromUtc: fromUtc,
            CreatedToUtc: fromUtc.AddMinutes(-1)));

        result.IsValid.Should().BeFalse();
    }
}
