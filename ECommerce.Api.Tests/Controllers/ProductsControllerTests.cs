using ECommerce.Api.Contracts.Products;
using ECommerce.Api.Controllers;
using ECommerce.Application.Products;
using ECommerce.Application.Products.Dtos;
using ECommerce.Shared.Pagination;
using ECommerce.Shared.Results;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ECommerce.Api.Tests.Controllers;

public sealed class ProductsControllerTests
{
    [Fact]
    public async Task GetAll_WhenQueryValidationFails_ReturnsBadRequestWithErrors()
    {
        var errors = new[] { "PageSize must be between 1 and 100." };
        var mediator = new Mock<ISender>();
        mediator.Setup(sender => sender.Send(
                It.IsAny<GetProductsQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<ProductDto>>.Failure(errors));
        var controller = new ProductsController(mediator.Object);

        var response = await controller.GetAll(
            new ProductSearchRequest { PageSize = 101 },
            CancellationToken.None);

        response.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().BeEquivalentTo(errors);
    }
}
