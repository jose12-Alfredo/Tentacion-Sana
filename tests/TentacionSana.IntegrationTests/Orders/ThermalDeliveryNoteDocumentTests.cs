using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TentacionSana.Application.Orders;
using TentacionSana.Infrastructure.Orders;

namespace TentacionSana.IntegrationTests.Orders;

public sealed class ThermalDeliveryNoteDocumentTests
{
    [Theory]
    [InlineData("AESGYM", "AESGYM BOLIVAR", "Bolivar", "BOLIVAR")]
    [InlineData("AESGYM", "AESGYM · BOLÍVAR", "bolivar", "BOLÍVAR")]
    [InlineData("Cliente de ejemplo", "Cliente de ejemplo - Central", " Central ", "Central")]
    public void OmitsOnlyRedundantRecipientText(string customer, string point, string address, string compactPoint)
    {
        var note = Sample("normal") with { Customer = customer, Point = point, Address = address };
        var expected = note with { Point = compactPoint, Address = null };
        Assert.Equal(ThermalDeliveryNoteDocument.Generate(expected, "NE-000001").Png,
            ThermalDeliveryNoteDocument.Generate(note, "NE-000001").Png);
    }

    [Fact]
    public void KeepsStreetNumbersAndDistinctBranchNames()
    {
        var note = Sample("normal") with { Customer = "GYM", Point = "GYMNASIO BOLIVAR", Address = "Calle Bolívar 123" };
        Assert.NotEqual(ThermalDeliveryNoteDocument.Generate(note, "NE-000001").Png,
            ThermalDeliveryNoteDocument.Generate(note with { Address = null }, "NE-000001").Png);
        Assert.NotEqual(ThermalDeliveryNoteDocument.Generate(note, "NE-000001").Png,
            ThermalDeliveryNoteDocument.Generate(note with { Point = "NASIO BOLIVAR" }, "NE-000001").Png);
    }

    [Theory]
    [InlineData("normal-sin-adicionales")]
    [InlineData("degustacion-publicidad")]
    [InlineData("reposicion")]
    [InlineData("multiples-adicionales")]
    [InlineData("pendiente-visible")]
    [InlineData("pendiente-oculto")]
    [InlineData("pago-parcial")]
    [InlineData("completamente-pagado")]
    [InlineData("entrega-parcial")]
    public void GeneratesFinalCompactSamplesAndReportsTheirPhysicalLength(string sample)
    {
        var note = FinalSample(sample);
        var files = ThermalDeliveryNoteDocument.Generate(note, "NE-000001");
        var media = Regex.Match(Encoding.Latin1.GetString(files.Pdf), @"/MediaBox\s*\[\s*0\s+0\s+([\d.]+)\s+([\d.]+)\s*\]");
        Assert.True(media.Success);
        var heightMm = double.Parse(media.Groups[2].Value, CultureInfo.InvariantCulture) * 25.4 / 72;
        var image = ThermalPng.Decode(files.Png);
        Assert.Equal(384, image.Width); Assert.Equal(1, files.Png[24]);
        Assert.InRange(Math.Abs(image.Height * 48d / 384 - heightMm), 0, .5);
        var output = Environment.GetEnvironmentVariable("TENTACION_THERMAL_SAMPLES");
        if (!string.IsNullOrWhiteSpace(output))
        {
            var folder = Path.Combine(output, "ajuste-final"); Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, sample + ".pdf"), files.Pdf);
            File.WriteAllBytes(Path.Combine(folder, sample + ".png"), files.Png);
            File.WriteAllText(Path.Combine(folder, sample + ".json"), System.Text.Json.JsonSerializer.Serialize(new
            {
                Sample = sample, HeightMm = heightMm, PngWidth = image.Width, PngHeight = image.Height, Content = note
            }));
        }
    }

    [Fact]
    public void HistoricalSummaryUsesRegisteredQuantitiesAndOriginalDeliveryDates()
    {
        var note = FinalSample("entrega-parcial");
        var orderLine = new NoteOrderLineSource(note.Lines[0].OrderLineId, note.Lines[0].ProductName, 15, 1, 2, 17);
        var scheduled = new DateTimeOffset(2026, 10, 28, 14, 0, 0, TimeSpan.Zero);
        var completed = scheduled.AddDays(1);
        var delivery = new NoteDeliverySource(Guid.NewGuid(), "Delivered", 1, scheduled, completed, null, null,
            [new(orderLine.Id, 5, 1, 1, 5, 1, 1)]);
        var lines = DeliveryNoteProjection.SelectLines([orderLine], [delivery], delivery.Id, true);
        var summary = note with { Scope = "RegisteredSummary", DeliveryId = delivery.Id, Lines = lines, ScheduledAtUtc = scheduled, CompletedAtUtc = completed };
        Assert.Equal(7, summary.PhysicalUnits); Assert.Equal(85, summary.Subtotal);
        var files = ThermalDeliveryNoteDocument.Generate(summary, "NE-000001");
        var output = Environment.GetEnvironmentVariable("TENTACION_THERMAL_SAMPLES");
        if (!string.IsNullOrWhiteSpace(output))
        {
            var folder = Path.Combine(output, "ajuste-final"); Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "verificacion-historica.pdf"), files.Pdf);
            File.WriteAllBytes(Path.Combine(folder, "verificacion-historica.png"), files.Png);
        }
    }

    private static ThermalNoteContent FinalSample(string sample)
    {
        var source = sample switch
        {
            "degustacion-publicidad" => "publicidad",
            "reposicion" => "reposiciones",
            "multiples-adicionales" => "adicionales",
            _ => "normal"
        };
        var note = Sample(source) with { TemplateVersion = "58mm-v2.1", DeliveryFee = 0,
            Customer = "Cliente de ejemplo", Point = "Cliente de ejemplo · Central", Address = "Central",
            IssuedAtUtc = new(2026, 11, 25, 14, 0, 0, TimeSpan.Zero), ScheduledAtUtc = new(2026, 11, 26, 14, 0, 0, TimeSpan.Zero) };
        if (sample == "entrega-parcial")
        {
            var line = note.Lines[0]; var id = Guid.NewGuid();
            var scheduled = new DateTimeOffset(2026, 11, 24, 14, 0, 0, TimeSpan.Zero);
            var delivery = new NoteDeliverySource(id, "Partial", 2, scheduled, scheduled, null, null,
                [new(line.OrderLineId, 5, 1, 2, 2, 0, 1)]);
            var other = new NoteDeliverySource(Guid.NewGuid(), "Scheduled", 1, scheduled.AddDays(3), null, null, null,
                [new(line.OrderLineId, 10, 0, 0, 0, 0, 0)]);
            var lines = DeliveryNoteProjection.SelectLines([new(line.OrderLineId, line.ProductName, 15, 1, 2, 17,
                "Acuerdo comercial", "Reposición autorizada", "Publicidad", "Unidad para video promocional.")], [delivery, other], id, false);
            note = note with { DeliveryId = id, Lines = lines, ScheduledAtUtc = scheduled, CompletedAtUtc = null };
            Assert.Equal(3, note.SaleUnits); Assert.Equal(1, note.ReplacementUnits); Assert.Equal(1, note.TastingUnits);
        }
        var show = sample is "pendiente-visible" or "pago-parcial" or "completamente-pagado";
        var paid = sample == "completamente-pagado" ? note.Total : sample == "pago-parcial" ? 100 : 0;
        var payment = DeliveryNotePaymentProjection.Evaluate(show, note.Total, note.Total, paid, true, paid);
        return note with { ShowPaymentStatus = show, PaymentLabel = payment.Label, PaymentWarning = payment.Warning };
    }

    [Fact]
    public void UsesTheSuppliedCircularLogoWithoutFilters()
    {
        var logo = Sample("normal").Logo;
        Assert.Equal("FEE0B9458EE25E63DD64AF1AFD165F6F40B561D1AC68E7299536B4DD8D3AF394", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(logo)));
        var image = ThermalPng.Decode(logo);
        Assert.Equal(256, image.Width); Assert.Equal(256, image.Height);
    }
    [Theory]
    [InlineData("normal")]
    [InlineData("adicionales")]
    [InlineData("motivos-extensos")]
    [InlineData("entrega-parcial")]
    [InlineData("solo-sin-cargo")]
    [InlineData("degustaciones")]
    [InlineData("reposiciones")]
    [InlineData("publicidad")]
    [InlineData("pago-pendiente")]
    [InlineData("pago-parcial")]
    [InlineData("sin-informacion-pago")]
    public void GeneratesReal58MmPdfAndOneBit384PixelPng(string sample)
    {
        var note = Sample(sample);
        var files = ThermalDeliveryNoteDocument.Generate(note, "NE-000001");
        Assert.Equal("%PDF", Encoding.ASCII.GetString(files.Pdf, 0, 4));
        var match = Regex.Match(Encoding.Latin1.GetString(files.Pdf), @"/MediaBox\s*\[\s*0\s+0\s+([\d.]+)\s+([\d.]+)\s*\]");
        Assert.True(match.Success, "El PDF debe contener su ancho físico en MediaBox.");
        Assert.InRange(double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), 164.3, 164.5);
        Assert.Equal(384, BinaryPrimitives.ReadInt32BigEndian(files.Png.AsSpan(16, 4)));
        Assert.Equal(1, files.Png[24]); Assert.Equal(0, files.Png[25]);
        var decoded = ThermalPng.Decode(files.Png);
        Assert.True(decoded.Height > 200);
        Assert.All(decoded.Rgba, x => Assert.True(x is 0 or 255));
        var output = Environment.GetEnvironmentVariable("TENTACION_THERMAL_SAMPLES");
        if (!string.IsNullOrWhiteSpace(output))
        {
            Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output, sample + ".pdf"), files.Pdf);
            File.WriteAllBytes(Path.Combine(output, sample + ".png"), files.Png);
        }
    }
    [Fact]
    public void OptionalPaymentBoxDisappearsWithoutLeavingBlankSpace()
    {
        var note = Sample("normal");
        var hidden = ThermalDeliveryNoteDocument.Generate(note, "NE-000001").Png;
        var omitted = ThermalDeliveryNoteDocument.Generate(note with { ShowPaymentStatus = true }, "NE-000001").Png;
        var visible = ThermalDeliveryNoteDocument.Generate(note with { ShowPaymentStatus = true, PaymentLabel = "PENDIENTE DE PAGO" }, "NE-000001").Png;
        Assert.Equal(hidden, omitted);
        Assert.True(ThermalPng.Decode(visible).Height > ThermalPng.Decode(hidden).Height);
        var switchedOff = ThermalDeliveryNoteDocument.Generate(note with { ShowPaymentStatus = false, PaymentLabel = "PENDIENTE DE PAGO" }, "NE-000001").Png;
        Assert.Equal(hidden, switchedOff);
    }

    [Fact]
    public void NeverRendersPaidLabelOrPaymentBoxForOnlyFreeProducts()
    {
        var note = Sample("normal");
        var plain = ThermalDeliveryNoteDocument.Generate(note, "NE-000001").Png;
        Assert.Equal(plain, ThermalDeliveryNoteDocument.Generate(note with { ShowPaymentStatus = true, PaymentLabel = "PAGADO" }, "NE-000001").Png);
        var free = Sample("solo-sin-cargo");
        Assert.Equal(ThermalDeliveryNoteDocument.Generate(free, "NE-000001").Png,
            ThermalDeliveryNoteDocument.Generate(free with { ShowPaymentStatus = true, PaymentLabel = "PENDIENTE DE PAGO" }, "NE-000001").Png);
    }

    [Fact]
    public void DigitalWidthIsConfigurableIndependentlyOfPaperWidth()
    {
        var files = ThermalDeliveryNoteDocument.Generate(Sample("normal") with { PixelWidth = 576 }, "NE-000002");
        Assert.Equal(576, ThermalPng.Decode(files.Png).Width);
    }
    [Fact]
    public void RejectsMalformedPng()
    {
        Assert.Throws<ArgumentException>(() => ThermalPng.Decode([1, 2, 3]));
    }
    internal static ThermalNoteContent Sample(string sample)
    {
        var free = sample is "adicionales" or "motivos-extensos" or "entrega-parcial" or "solo-sin-cargo" or "degustaciones" or "reposiciones" or "publicidad";
        var longText = string.Join(" ", Enumerable.Repeat("Observación de promoción: degustación y reposición para la recepción del cliente, con tildes y ñ.", 5));
        var lines = new List<ThermalNoteLine> { new(Guid.NewGuid(), "Budín proteico chocolate · nombre histórico", sample == "solo-sin-cargo" ? 0 : sample == "entrega-parcial" ? 3 : 15,
            free && sample != "degustaciones" && sample != "publicidad" ? 1 : 0, free && sample != "reposiciones" ? 2 : 0, 17, "Acuerdo comercial", "Reposición autorizada", sample == "publicidad" ? "Publicidad" : "Degustación en punto de venta", sample == "publicidad" ? "Unidad destinada a la grabación de un video promocional." : "Para que los clientes conozcan el producto.") };
        if (sample == "motivos-extensos")
        {
            lines[0] = lines[0] with { ProductName = "Budín proteico de chocolate con denominación histórica extensa y caracteres especiales: ñ, á, é, í, ó, ú", TastingNotes = longText, ReplacementNotes = longText };
            lines.Add(new(Guid.NewGuid(), lines[0].ProductName, 0, 0, 1, 20, null, null, "Promoción", "Producto destinado a la grabación de un video publicitario."));
        }
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/TentacionSana.Web"));
        var logoPath = Path.Combine(root, "wwwroot/images/brand/logo_tentacion_sana_termico.png");
        var logo = File.ReadAllBytes(logoPath);
        return new(Guid.NewGuid(), 900001, Guid.NewGuid(), "PendingDelivery", "1", new(2026, 10, 29, 14, 0, 0, TimeSpan.Zero),
            sample == "motivos-extensos" ? "Cliente de ejemplo con nombre comercial extenso para validar el ajuste automático de palabras y caracteres españoles" : "Cliente de ejemplo",
            "Sucursal de ejemplo", "Dirección comercial de ejemplo", null, "", null, new(2026, 10, 29, 14, 0, 0, TimeSpan.Zero), null,
             lines, sample is "entrega-parcial" or "solo-sin-cargo" ? 0 : 10, "78969873", 384, 58, 48, logo)
             { TemplateVersion = "58mm-v2", ShowPaymentStatus = sample is "pago-pendiente" or "pago-parcial",
                 PaymentLabel = sample == "pago-pendiente" ? "PENDIENTE DE PAGO" : sample == "pago-parcial" ? "PAGO PARCIAL" : null };
    }
}
