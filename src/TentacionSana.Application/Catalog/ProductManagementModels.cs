namespace TentacionSana.Application.Catalog;

public sealed record CreateProductRequest(
    string Name,
    string Slug,
    string Presentation,
    string PublicDescription,
    decimal StandardPrice,
    bool ShowPublicPrice,
    bool Publish,
    bool IsFeatured,
    string? ApprovedBenefits,
    bool IsLandingFeatured,
    int LandingOrder,
    string? LandingTitle,
    string? LandingSubtitle,
    string? LandingBadgeText);

public sealed record ManagedProductSummary(
    Guid Id,
    string Name,
    string Slug,
    string Presentation,
    decimal StandardPrice,
    bool IsActive,
    bool IsPublished,
    bool IsFeatured,
    string PublicDescription,
    bool ShowPublicPrice,
    string? ApprovedBenefits,
    bool IsLandingFeatured,
    int LandingOrder,
    string? LandingTitle,
    string? LandingSubtitle,
    string? LandingBadgeText,
    string? CategoryName,
    string? ImageUrl,
    string? ImageAltText);

public sealed record CreateProductResult(bool Succeeded, Guid? ProductId, IReadOnlyList<string> Errors);

public sealed record ProductStateResult(bool Succeeded, IReadOnlyList<string> Errors);
