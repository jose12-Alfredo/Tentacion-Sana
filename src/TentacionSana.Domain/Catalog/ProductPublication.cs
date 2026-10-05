namespace TentacionSana.Domain.Catalog;

public sealed class ProductPublication
{
    private ProductPublication() { }

    public ProductPublication(Guid productId)
    {
        ProductId = productId;
    }

    public Guid ProductId { get; private set; }
    public string PublicDescription { get; private set; } = string.Empty;
    public string? ApprovedBenefits { get; private set; }
    public bool IsPublished { get; private set; }
    public bool IsFeatured { get; private set; }
    public bool IsLandingFeatured { get; private set; }
    public bool ShowPublicPrice { get; private set; }
    public int DisplayOrder { get; private set; }
    public int LandingOrder { get; private set; }
    public string? LandingTitle { get; private set; }
    public string? LandingSubtitle { get; private set; }
    public string? LandingBadgeText { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }

    public void Configure(
        string publicDescription,
        string? approvedBenefits,
        bool isFeatured,
        bool isLandingFeatured,
        bool showPublicPrice,
        int displayOrder,
        int landingOrder,
        string? landingTitle,
        string? landingSubtitle,
        string? landingBadgeText)
    {
        PublicDescription = publicDescription.Trim();
        ApprovedBenefits = string.IsNullOrWhiteSpace(approvedBenefits) ? null : approvedBenefits.Trim();
        IsFeatured = isFeatured;
        IsLandingFeatured = isLandingFeatured;
        ShowPublicPrice = showPublicPrice;
        DisplayOrder = displayOrder;
        LandingOrder = landingOrder;
        LandingTitle = Clean(landingTitle);
        LandingSubtitle = Clean(landingSubtitle);
        LandingBadgeText = Clean(landingBadgeText);
    }

    public void Configure(
        string publicDescription,
        string? approvedBenefits,
        bool isFeatured,
        bool showPublicPrice,
        int displayOrder) =>
        Configure(publicDescription, approvedBenefits, isFeatured, false, showPublicPrice,
            displayOrder, 0, null, null, null);

    public void Publish(DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(PublicDescription))
        {
            throw new InvalidOperationException("Se requiere una descripción pública antes de publicar.");
        }

        IsPublished = true;
        PublishedAtUtc = nowUtc;
    }

    public void Hide() => IsPublished = false;

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
