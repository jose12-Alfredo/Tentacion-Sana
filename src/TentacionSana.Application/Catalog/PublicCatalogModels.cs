namespace TentacionSana.Application.Catalog;

public sealed record PublicProductSummary(
    Guid Id,
    string Name,
    string Slug,
    string Presentation,
    string Description,
    decimal? PublicPrice,
    string? ImageUrl,
    string? ImageAltText,
    bool IsFeatured);

public sealed record PublicProductDetail(
    Guid Id,
    string Name,
    string Slug,
    string Presentation,
    string Description,
    string? ApprovedBenefits,
    decimal? PublicPrice,
    IReadOnlyList<PublicProductImage> Images);

public sealed record PublicProductImage(string Url, string AltText, int Width, int Height);

public sealed record LandingProductSlide(
    Guid Id,
    string Name,
    string Slug,
    string Presentation,
    string Description,
    string? ApprovedBenefits,
    decimal? PublicPrice,
    string ImageUrl,
    string ImageAltText,
    int ImageWidth,
    int ImageHeight,
    string? LandingTitle,
    string? LandingSubtitle,
    string? LandingBadgeText,
    int LandingOrder);
