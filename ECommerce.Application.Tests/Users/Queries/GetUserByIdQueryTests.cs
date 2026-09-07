using ECommerce.Application.Users;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerce.Application.Tests.Users.Queries;

public class GetUserByIdQueryTests
{
    [Fact]
    public void GetUserByIdQuery_ReturnsRecord()
    {
        // Arrange
        var userId = Guid.NewGuid();
        GetUserByIdQuery query = new(userId);
        // Assert
        query.UserId.Should().Be(userId);
    }
}
