namespace ECommerce.Api.Contracts.Products;

public sealed record CreateProductRequest(string Name, string Description, decimal Price, int Stock);
public sealed record UpdateProductRequest(string Name, string Description, decimal Price, int Stock);

public sealed class ProductSearchRequest
{
    public string? Search { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public string SortBy { get; init; } = "name";
    public bool Descending { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
