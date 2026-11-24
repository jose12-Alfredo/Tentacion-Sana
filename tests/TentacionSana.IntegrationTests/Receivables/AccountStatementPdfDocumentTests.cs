using System.Text;
using TentacionSana.Application.Receivables;
using TentacionSana.Infrastructure.Receivables;

namespace TentacionSana.IntegrationTests.Receivables;

public sealed class AccountStatementPdfDocumentTests
{
    [Fact]
    public void OrdersByDeliveryDateAcrossBranchesWithSameDayTiesAndMissingDatesLast()
    {
        var later = Order(1001, "Abasto Central", 100) with { DeliveredAtUtc = new DateTimeOffset(2026, 10, 29, 4, 0, 0, TimeSpan.Zero) };
        var earlier = Order(1100, "Urubó", 200) with { DeliveredAtUtc = new DateTimeOffset(2026, 10, 13, 16, 0, 0, TimeSpan.Zero) };
        var sameDayLowerNumber = Order(1090, "Palacio", 300) with { DeliveredAtUtc = new DateTimeOffset(2026, 10, 13, 23, 0, 0, TimeSpan.Zero) };
        var missing = Order(900, "Biomarket", 50) with { DeliveredAtUtc = null };
        var selected = ReceivablesService.SelectStatementOrders([later, earlier, missing, sameDayLowerNumber], null);
        Assert.Equal([1090, 1100, 1001, 900], selected.Select(x => x.OrderNumber));
        Assert.Equal(650m, selected.Sum(x => x.Balance));
        var subset = ReceivablesService.SelectStatementOrders([later, earlier, missing, sameDayLowerNumber], [later.OrderId, earlier.OrderId]);
        Assert.Equal([1100, 1001], subset.Select(x => x.OrderNumber));
    }

    [Fact]
    public void SummaryAndAttachmentsUseSameDeliveryOrderAndBolivianCalendarDate()
    {
        // 02:00 UTC del 14 todavía corresponde al 13 en Bolivia.
        var earlier = Order(1100, "Urubó", 200) with { DeliveredAtUtc = new DateTimeOffset(2026, 10, 14, 2, 0, 0, TimeSpan.Zero) };
        var later = Order(1001, "Abasto Central", 100) with { DeliveredAtUtc = new DateTimeOffset(2026, 10, 14, 4, 0, 0, TimeSpan.Zero) };
        var payer = new PayerAccountItem(Guid.NewGuid(), "Central", null, "", ["Nutriarte"], 300, 0, 300, [later, earlier]);
        var model = new AccountStatementPdfModel("EC-PRUEBA", DateTimeOffset.UtcNow, "Administración", payer, [later, earlier], "Current", null, null, 300, [], [],
            [new(1001, "BOLETA DE ENTREGA", later.DeliveredAtUtc.Value, []),
             new(1100, "PAGO PARCIAL", DateTimeOffset.UtcNow, []),
             new(1100, "BOLETA DE ENTREGA", earlier.DeliveredAtUtc.Value, [])]);
        var sorted = AccountStatementPdfDocument.InChronologicalOrder(model);
        Assert.Equal([1100, 1001], sorted.Orders.Select(x => x.OrderNumber));
        Assert.Equal([1100, 1100, 1001], sorted.Evidence.Select(x => x.OrderNumber));
        Assert.Equal("BOLETA DE ENTREGA", sorted.Evidence[0].Type);
        Assert.Equal(300m, sorted.CurrentBalance);
    }

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
        var earlier = Order(1111, "Urubó", 60) with { DeliveredAtUtc = DateTimeOffset.UtcNow.AddDays(-8) };
        var later = Order(1005, "Palacio", 40) with { DeliveredAtUtc = DateTimeOffset.UtcNow.AddDays(-1) };
        var payer = new PayerAccountItem(Guid.NewGuid(), "Contabilidad Nutriarte", "Sandra L\u00F3pez", "75000000", ["Nutriarte"], 400, 20, 380, [later, order, earlier]);
        var model = new AccountStatementPdfModel("EC-NUTRIARTE-20261004-001", DateTimeOffset.UtcNow, "Administraci\u00F3n", payer, [later, order, earlier], "Current", 580, 200, 380, logo, [],
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
