using ECommerce.Api.Contracts.Products;
using ECommerce.Application.Products;
using ECommerce.Application.Products.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers;

public sealed class ProductsController(ISender mediator) : BaseApiController
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyCollection<ProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProductsQuery(), cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("{productId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid productId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProductByIdQuery(productId), cancellationToken);
        return result.IsFailure ? NotFound(result.Errors) : Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new CreateProductCommand(request.Name, request.Description, request.Price, request.Stock), cancellationToken);
        if (result.IsFailure) return BadRequest(result.Errors);

        return CreatedAtAction(nameof(GetById), new { productId = result.Value!.Id }, result.Value);
    }

    [HttpPut("{productId:guid}")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid productId, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new UpdateProductCommand(productId, request.Name, request.Description, request.Price, request.Stock), cancellationToken);
        return result.IsFailure
            ? result.Errors.Contains("Product not found.") ? NotFound(result.Errors) : BadRequest(result.Errors)
            : Ok(result.Value);
    }

    [HttpDelete("{productId:guid}")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid productId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteProductCommand(productId), cancellationToken);
        return result.IsFailure ? NotFound(result.Errors) : NoContent();
    }
}
