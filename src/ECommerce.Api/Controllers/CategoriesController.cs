using ECommerce.Api.Contracts.Categories;
using ECommerce.Application.Categories;
using ECommerce.Application.Categories.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers;

public sealed class CategoriesController(ISender mediator) : BaseApiController
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyCollection<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetCategoriesQuery(), cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("{categoryId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid categoryId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetCategoryByIdQuery(categoryId), cancellationToken);
        return result.IsFailure ? NotFound(result.Errors) : Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CreateCategoryCommand(request.Name), cancellationToken);
        if (result.IsFailure)
            return BadRequest(result.Errors);

        return CreatedAtAction(nameof(GetById), new { categoryId = result.Value!.Id }, result.Value);
    }

    [HttpPut("{categoryId:guid}")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid categoryId,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new UpdateCategoryCommand(categoryId, request.Name),
            cancellationToken);
        return result.IsFailure
            ? result.Errors.Contains("Category not found.")
                ? NotFound(result.Errors)
                : BadRequest(result.Errors)
            : Ok(result.Value);
    }

    [HttpDelete("{categoryId:guid}")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid categoryId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteCategoryCommand(categoryId), cancellationToken);
        return result.IsFailure ? NotFound(result.Errors) : NoContent();
    }
}
