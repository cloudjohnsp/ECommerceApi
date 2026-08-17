using ECommerce.Domain.Enums;
using FluentAssertions;

namespace ECommerce.Domain.Tests.Enums;

public class UserRoleTests
{
    [Fact]
    public void Customer_HasValueOne()
    {
        ((int)UserRole.Customer).Should().Be(1);
    }

    [Fact]
    public void Administrator_HasValueTwo()
    {
        ((int)UserRole.Administrator).Should().Be(2);
    }
}
