using System.Linq.Expressions;
using ECommerce.Application.Abstractions.Specifications;
using ECommerce.Domain.Entities;

namespace ECommerce.Application.Products.Specifications;

public sealed class ProductCatalogSpecification : ISpecification<Product>
{
    public ProductCatalogSpecification(GetProductsQuery query)
    {
        var search = query.Search?.Trim().ToLowerInvariant();
        Criteria = product =>
            product.IsActive &&
            (search == null ||
                product.Name.ToLower().Contains(search) ||
                product.Description.ToLower().Contains(search)) &&
            (!query.MinPrice.HasValue || product.Price >= query.MinPrice.Value) &&
            (!query.MaxPrice.HasValue || product.Price <= query.MaxPrice.Value);
        ApplyOrdering = CreateOrdering(query.SortBy, query.Descending);
        Skip = (query.Page - 1) * query.PageSize;
        Take = query.PageSize;
    }

    public Expression<Func<Product, bool>> Criteria { get; }
    public Func<IQueryable<Product>, IOrderedQueryable<Product>> ApplyOrdering { get; }
    public int Skip { get; }
    public int Take { get; }

    private static Func<IQueryable<Product>, IOrderedQueryable<Product>> CreateOrdering(
        string sortBy,
        bool descending) => (sortBy.ToLowerInvariant(), descending) switch
        {
            ("price", false) => products => products.OrderBy(product => product.Price),
            ("price", true) => products => products.OrderByDescending(product => product.Price),
            ("createdat", false) => products => products.OrderBy(product => product.CreatedAt),
            ("createdat", true) => products => products.OrderByDescending(product => product.CreatedAt),
            ("name", true) => products => products.OrderByDescending(product => product.Name),
            _ => products => products.OrderBy(product => product.Name)
        };
}
