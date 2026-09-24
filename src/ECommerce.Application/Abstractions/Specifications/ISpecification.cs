using System.Linq.Expressions;

namespace ECommerce.Application.Abstractions.Specifications;

public interface ISpecification<T>
{
    Expression<Func<T, bool>> Criteria { get; }
    Func<IQueryable<T>, IOrderedQueryable<T>> ApplyOrdering { get; }
    int Skip { get; }
    int Take { get; }
}
