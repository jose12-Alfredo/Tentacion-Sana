using TentacionSana.Domain.Catalog;

namespace TentacionSana.UnitTests.Catalog;

public sealed class ProductPublicationTests
{
    [Fact]
    public void ProductCannotBePublishedWithoutPublicDescription()
    {
        var publication = new ProductPublication(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => publication.Publish(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void HidingProductDoesNotRemoveItsPublicationData()
    {
        var publication = new ProductPublication(Guid.NewGuid());
        publication.Configure("Descripción aprobada", null, true, true, 1);
        publication.Publish(DateTimeOffset.UtcNow);

        publication.Hide();

        Assert.False(publication.IsPublished);
        Assert.Equal("Descripción aprobada", publication.PublicDescription);
        Assert.True(publication.IsFeatured);
    }

    [Fact]
    public void LandingConfigurationIsStoredAndOptionalTextIsNormalized()
    {
        var publication = new ProductPublication(Guid.NewGuid());

        publication.Configure(
            "Descripción aprobada", "23 g proteína, Sin azúcar", false, true, true,
            3, 2, "  Energía real  ", "  Para tu día  ", "  Nuevo sabor  ");

        Assert.True(publication.IsLandingFeatured);
        Assert.Equal(2, publication.LandingOrder);
        Assert.Equal("Energía real", publication.LandingTitle);
        Assert.Equal("Para tu día", publication.LandingSubtitle);
        Assert.Equal("Nuevo sabor", publication.LandingBadgeText);
    }
}
