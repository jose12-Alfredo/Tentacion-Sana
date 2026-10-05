namespace TentacionSana.Application.Catalog;

public interface IPublicCatalogService
{
    Task<IReadOnlyList<PublicProductSummary>> GetPublishedProductsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LandingProductSlide>> GetLandingProductsAsync(
        CancellationToken cancellationToken = default);

    Task<PublicProductDetail?> GetPublishedProductAsync(
        string slug,
        CancellationToken cancellationToken = default);
}
