namespace TentacionSana.Application.Catalog;

public interface IProductManagementService
{
    Task<IReadOnlyList<ManagedProductSummary>> GetProductsAsync(
        CancellationToken cancellationToken = default);

    Task<CreateProductResult> CreateProductAsync(
        CreateProductRequest request,
        Guid? userId,
        CancellationToken cancellationToken = default);

    Task<CreateProductResult> UpdateProductAsync(
        Guid productId,
        CreateProductRequest request,
        Guid? userId,
        CancellationToken cancellationToken = default);

    Task<ProductStateResult> SetActiveAsync(
        Guid productId,
        bool isActive,
        Guid? userId,
        CancellationToken cancellationToken = default);

    Task<ProductStateResult> SetPublishedAsync(
        Guid productId,
        bool isPublished,
        Guid? userId,
        CancellationToken cancellationToken = default);
}
