using ECommerce.Api.Contracts.Products;
using ECommerce.Application.Products;
using ECommerce.Application.Products.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ECommerce.Shared.Pagination;
using ECommerce.Domain.Entities;

namespace ECommerce.Api.Controllers;

public sealed class ProductsController(ISender mediator) : BaseApiController
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] ProductSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProductsQuery(
            request.Search,
            request.MinPrice,
            request.MaxPrice,
            request.SortBy,
            request.Descending,
            request.Page,
            request.PageSize,
            request.CategoryId), cancellationToken);
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

    [HttpGet("{productId:guid}/images")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyCollection<ProductImageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImages(Guid productId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProductImagesQuery(productId), cancellationToken);
        return result.IsFailure ? NotFound(result.Errors) : Ok(result.Value);
    }

    [HttpPost("{productId:guid}/images")]
    [Authorize(Roles = "Administrator")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ProductImage.MaximumSizeBytes + 64 * 1024)]
    [ProducesResponseType(typeof(ProductImageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UploadImage(
        Guid productId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        var result = await mediator.Send(
            new UploadProductImageCommand(
                productId,
                content,
                file.FileName,
                file.ContentType,
                file.Length),
            cancellationToken);
        if (result.IsSuccess)
            return CreatedAtAction(nameof(GetImages), new { productId }, result.Value);
        if (result.Errors.Contains("Product not found."))
            return NotFound(result.Errors);
        if (result.Errors.Contains("Product image storage is unavailable.") ||
            result.Errors.Contains("Product image storage is disabled."))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, result.Errors);
        }

        return BadRequest(result.Errors);
    }

    [HttpPost]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new CreateProductCommand(
                request.Name,
                request.Description,
                request.Price,
                request.Stock,
                request.CategoryId), cancellationToken);
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
            new UpdateProductCommand(
                productId,
                request.Name,
                request.Description,
                request.Price,
                request.Stock,
                request.CategoryId), cancellationToken);
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
