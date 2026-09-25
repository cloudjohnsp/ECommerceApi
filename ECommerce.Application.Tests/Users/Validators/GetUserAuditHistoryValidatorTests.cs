using ECommerce.Application.Users;
using ECommerce.Application.Users.Validators;
using FluentAssertions;

namespace ECommerce.Application.Tests.Users.Validators;

public sealed class GetUserAuditHistoryValidatorTests
{
    private readonly GetUserAuditHistoryValidator _validator = new();

    [Fact]
    public void Validate_WithValidPagination_Succeeds()
    {
        var result = _validator.Validate(new GetUserAuditHistoryQuery(Guid.NewGuid(), 1, 100));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Validate_WithInvalidPagination_Fails(int page, int pageSize)
    {
        var result = _validator.Validate(new GetUserAuditHistoryQuery(Guid.NewGuid(), page, pageSize));

        result.IsValid.Should().BeFalse();
    }
}
