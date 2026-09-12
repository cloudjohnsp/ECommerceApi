namespace ECommerce.Application.Abstractions.Specifications;

public interface ISpecification<T>
{
    bool IsSatisfiedBy(T entity);
}
