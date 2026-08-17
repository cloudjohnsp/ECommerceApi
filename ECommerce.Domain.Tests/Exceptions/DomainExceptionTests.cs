using ECommerce.Domain.Exceptions;
using FluentAssertions;

namespace ECommerce.Domain.Tests.Exceptions;

public class DomainExceptionTests
{
    [Fact]
    public void Constructor_SetsMessage()
    {
        var exception = new DomainException("User cannot be deactivated.");

        exception.Should().BeOfType<DomainException>();
        exception.Message.Should().Be("User cannot be deactivated.");
    }
}
