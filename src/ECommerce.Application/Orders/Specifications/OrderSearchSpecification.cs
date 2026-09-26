using System.Linq.Expressions;
using ECommerce.Application.Abstractions.Specifications;
using ECommerce.Domain.Entities;

namespace ECommerce.Application.Orders.Specifications;

public sealed class OrderSearchSpecification : ISpecification<Order>
{
    public OrderSearchSpecification(SearchOrdersQuery query)
    {
        Criteria = order =>
            (!query.CustomerId.HasValue || order.CustomerId == query.CustomerId.Value) &&
            (!query.Status.HasValue || order.Status == query.Status.Value) &&
            (!query.CreatedFromUtc.HasValue || order.CreatedAt >= query.CreatedFromUtc.Value) &&
            (!query.CreatedToUtc.HasValue || order.CreatedAt < query.CreatedToUtc.Value);
        ApplyOrdering = CreateOrdering(query.SortBy, query.Descending);
        Skip = (query.Page - 1) * query.PageSize;
        Take = query.PageSize;
    }

    public Expression<Func<Order, bool>> Criteria { get; }
    public Func<IQueryable<Order>, IOrderedQueryable<Order>> ApplyOrdering { get; }
    public int Skip { get; }
    public int Take { get; }

    private static Func<IQueryable<Order>, IOrderedQueryable<Order>> CreateOrdering(
        string sortBy,
        bool descending) => (sortBy.ToLowerInvariant(), descending) switch
        {
            ("status", false) => orders => orders.OrderBy(order => order.Status),
            ("status", true) => orders => orders.OrderByDescending(order => order.Status),
            ("total", false) => orders => orders.OrderBy(order =>
                order.Items.Sum(item => item.UnitPrice * item.Quantity)),
            ("total", true) => orders => orders.OrderByDescending(order =>
                order.Items.Sum(item => item.UnitPrice * item.Quantity)),
            ("createdat", false) => orders => orders.OrderBy(order => order.CreatedAt),
            _ => orders => orders.OrderByDescending(order => order.CreatedAt)
        };
}
