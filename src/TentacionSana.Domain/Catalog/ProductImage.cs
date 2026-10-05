namespace TentacionSana.Domain.Catalog;

public sealed class ProductImage
{
    private ProductImage() { }

    public ProductImage(
        Guid id,
        Guid productId,
        string publicId,
        string secureUrl,
        string format,
        int width,
        int height,
        long bytes,
        string altText,
        bool isPrimary,
        int displayOrder,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        ProductId = productId;
        PublicId = publicId.Trim();
        SecureUrl = secureUrl.Trim();
        Format = format.Trim();
        Width = width;
        Height = height;
        Bytes = bytes;
        AltText = altText.Trim();
        IsPrimary = isPrimary;
        DisplayOrder = displayOrder;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string PublicId { get; private set; } = string.Empty;
    public string SecureUrl { get; private set; } = string.Empty;
    public string Format { get; private set; } = string.Empty;
    public int Width { get; private set; }
    public int Height { get; private set; }
    public long Bytes { get; private set; }
    public string AltText { get; private set; } = string.Empty;
    public bool IsPrimary { get; private set; }
    public int DisplayOrder { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public void SetAsPrimary() => IsPrimary = true;

    public void SetAsSecondary() => IsPrimary = false;
}
