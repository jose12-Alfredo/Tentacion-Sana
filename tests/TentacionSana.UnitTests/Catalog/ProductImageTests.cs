using TentacionSana.Domain.Catalog;

namespace TentacionSana.UnitTests.Catalog;

public sealed class ProductImageTests
{
    [Fact]
    public void ImageCanBePromotedAndDemotedAsProductMainImage()
    {
        var image = new ProductImage(
            Guid.NewGuid(), Guid.NewGuid(), "products/test", "https://example.com/test.webp",
            "webp", 800, 600, 1000, "Producto", false, 1, DateTimeOffset.UtcNow);

        image.SetAsPrimary();
        Assert.True(image.IsPrimary);

        image.SetAsSecondary();
        Assert.False(image.IsPrimary);
    }
}
