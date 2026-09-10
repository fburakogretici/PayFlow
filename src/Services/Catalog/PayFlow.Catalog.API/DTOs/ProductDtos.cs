namespace PayFlow.Catalog.API.DTOs;

public record ProductDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    int StockQuantity,
    string Category,
    DateTime CreatedAtUtc);

public record CreateProductRequest(
    string Name,
    string Description,
    decimal Price,
    int StockQuantity,
    string Category);

public record UpdateProductRequest(
    string Name,
    string Description,
    decimal Price,
    int StockQuantity,
    string Category);
