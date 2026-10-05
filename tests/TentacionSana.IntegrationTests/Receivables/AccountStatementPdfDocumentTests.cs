using System.Text;
using TentacionSana.Application.Receivables;
using TentacionSana.Infrastructure.Receivables;

namespace TentacionSana.IntegrationTests.Receivables;

public sealed class AccountStatementPdfDocumentTests
{
    [Fact]
    public void SelectsOnlyRequestedOrdersForAStatement()
    {
        var first = Order(1018, "Abasto Central", 280);
        var second = Order(1025, "Equipetrol", 180);
        var third = Order(1030, "Calacoto", 90);

        var selected = ReceivablesService.SelectStatementOrders([first, second, third], [first.OrderId, third.OrderId]);

        Assert.Equal([1018, 1030], selected.Select(x => x.OrderNumber));
        Assert.Equal(370, selected.Sum(x => x.Balance));
    }

    [Fact]
    public void GeneratesSummaryAndOnePagePerEvidence()
    {
        var orderId = Guid.NewGuid();
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var logoPath = Path.Combine(repositoryRoot, "src", "TentacionSana.Web", "wwwroot", "images", "brand", "logo-oficial-tentacion-sana.png");
        var logo = File.Exists(logoPath) ? File.ReadAllBytes(logoPath) : [];
        var evidenceImage = logo.Length > 0 ? logo : Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        var order = new ReceivableOrderItem(orderId, 1018, "Nutriarte", "Abasto Central", DateTimeOffset.UtcNow.AddDays(-3), 300, 20, 280, 2, 3, "Partial", 1, 1);
        var payer = new PayerAccountItem(Guid.NewGuid(), "Contabilidad Nutriarte", "Sandra L\u00F3pez", "75000000", ["Nutriarte"], 300, 20, 280, [order]);
        var model = new AccountStatementPdfModel("EC-NUTRIARTE-20261004-001", DateTimeOffset.UtcNow, "Administraci\u00F3n", payer, [order], "Current", 480, 200, 280, logo, [],
            [new AccountStatementEvidence(1018, "BOLETA DE ENTREGA", DateTimeOffset.UtcNow.AddDays(-3), evidenceImage), new AccountStatementEvidence(1018, "PAGO PARCIAL", DateTimeOffset.UtcNow, evidenceImage)]);

        var pdf = AccountStatementPdfDocument.Generate(model);
        var pages = AccountStatementPdfDocument.GeneratePreviewImages(model);

        Assert.True(pdf.Length > 1_000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.Equal(3, pages.Count);
        var previewPath = Environment.GetEnvironmentVariable("TENTACION_SANA_PDF_PREVIEW");
        if (!string.IsNullOrWhiteSpace(previewPath))
        {
            File.WriteAllBytes(previewPath, pdf);
            for (var index = 0; index < pages.Count; index++) File.WriteAllBytes(Path.ChangeExtension(previewPath, $"page-{index + 1}.png"), pages[index]);
        }
    }

    private static ReceivableOrderItem Order(long number, string branch, decimal balance) =>
        new(Guid.NewGuid(), number, "Nutriarte", branch, DateTimeOffset.UtcNow.AddDays(-3), balance, 0, balance, 1, 3, "Unpaid", 0, 0);
}
