namespace ECommerce.Api.Contracts.Products;

public sealed record CreateProductRequest(string Name, string Description, decimal Price, int Stock);
public sealed record UpdateProductRequest(string Name, string Description, decimal Price, int Stock);
