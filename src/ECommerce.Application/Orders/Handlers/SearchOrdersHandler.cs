using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Orders.Dtos;
using ECommerce.Application.Orders.Specifications;
using ECommerce.Shared.Pagination;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Orders.Handlers;

public sealed class SearchOrdersHandler(IOrderRepository repository)
    : IRequestHandler<SearchOrdersQuery, Result<PagedResult<OrderDto>>>
{
    public async Task<Result<PagedResult<OrderDto>>> Handle(
        SearchOrdersQuery request,
        CancellationToken cancellationToken)
    {
        var orders = await repository.SearchAsync(
            new OrderSearchSpecification(request),
            cancellationToken);
        var response = new PagedResult<OrderDto>(
            [.. orders.Items.Select(order => order.ToDto())],
            orders.Page,
            orders.PageSize,
            orders.TotalCount);
        return Result<PagedResult<OrderDto>>.Success(response);
    }
}
