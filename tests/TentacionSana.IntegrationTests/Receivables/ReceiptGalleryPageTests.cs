using TentacionSana.Application.Deliveries;
using TentacionSana.Web;

namespace TentacionSana.IntegrationTests.Receivables;

public sealed class ReceiptGalleryPageTests
{
    [Fact]
    public void GalleryHasDirectPhotoLinksAndEscapesCustomerAndFileNames()
    {
        var evidenceId = Guid.NewGuid();
        var html = ReceiptGalleryPage.Render(Guid.NewGuid(), 1052, "<script>alert(1)</script>", "Biomarket & Centro",
            [new EvidenceItem(evidenceId, "<img onerror=alert(1)>", DateTimeOffset.UtcNow)]);
        Assert.Contains($"/media/delivery-evidence/{evidenceId:N}", html);
        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<img onerror=", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("Biomarket &amp; Centro", html);
    }

    [Fact]
    public void MissingReceiptHasClearMessageAndLinkToOriginalOrder()
    {
        var id = Guid.NewGuid();
        var html = ReceiptGalleryPage.Render(id, 1052, "Nutriarte", "Biomarket", []);
        Assert.Contains("no tiene boletas adjuntas", html);
        Assert.Contains($"/app/pedidos/{id}", html);
    }
}
