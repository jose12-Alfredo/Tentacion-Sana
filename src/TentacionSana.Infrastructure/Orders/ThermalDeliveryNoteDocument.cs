using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using TentacionSana.Application.Deliveries;
using TentacionSana.Application.Orders;

namespace TentacionSana.Infrastructure.Orders;

public static class ThermalDeliveryNoteDocument
{
    private static readonly CultureInfo MoneyCulture = CultureInfo.GetCultureInfo("es-BO");
    private static string Money(decimal value) => $"Bs {value.ToString("N2", MoneyCulture)}";

    public static (byte[] Pdf, byte[] Png) Generate(ThermalNoteContent note, string number)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = false;
        var document = Document.Create(root => root.Page(page =>
        {
            page.ContinuousSize(note.PaperWidthMm, Unit.Millimetre);
            page.MarginHorizontal((note.PaperWidthMm - note.PrintableWidthMm) / 2, Unit.Millimetre);
            page.MarginVertical(2, Unit.Millimetre);
            page.DefaultTextStyle(x => x.FontFamily("Lato").FontSize(8).FontColor("#000000"));
            page.PageColor("#FFFFFF");
            page.Content().PaddingHorizontal(.5f, Unit.Millimetre).Column(column =>
            {
                column.Spacing(2.5f);
                // 31 mm / 48 mm = 248 px a 384 px útiles. Se conserva la proporción del recurso.
                if (note.Logo.Length > 0) column.Item().AlignCenter().Width(note.PrintableWidthMm * 248 / 384, Unit.Millimetre).Image(note.Logo).FitWidth();
                column.Item().PaddingTop(3).AlignCenter().Text("NOTA DE ENTREGA").Bold().FontSize(11.5f);
                column.Item().AlignCenter().Text(number == "VISTA PREVIA" ? number : $"N.º {number}").Bold().FontSize(9);
                Line(column);
                column.Item().Text($"Pedido: #{note.OrderNumber}").Bold();
                column.Item().Text($"Emisión: {note.IssuedAtUtc.ToOffset(DeliveryDates.BusinessOffset):dd/MM/yyyy}");
                if (note.ScheduledAtUtc is { } scheduled) column.Item().Text($"Entrega programada: {DeliveryDates.LocalDate(scheduled):dd/MM/yyyy}").FontSize(7.5f);
                if (note.CompletedAtUtc is { } actual) column.Item().Text($"Entrega realizada: {DeliveryDates.LocalDate(actual):dd/MM/yyyy}").FontSize(7.5f);
                if (note.Scope == "RegisteredSummary") column.Item().Text("Resumen de entrega").Bold().FontSize(7.5f);
                column.Item().Text($"Cliente: {note.Customer}").Bold();
                var point = CompactPoint(note.Customer, note.Point);
                if (!string.IsNullOrWhiteSpace(point)) column.Item().Text($"Sucursal: {point}");
                if (!string.IsNullOrWhiteSpace(note.Address) && !Contained(note.Address, note.Customer) && !Contained(note.Address, note.Point))
                    column.Item().Text($"Dirección: {note.Address}").FontSize(7.5f);
                Line(column);
                var sales = note.Lines.Where(x => x.SaleQuantity > 0).ToList();
                if (sales.Count > 0)
                {
                    column.Item().Text("PRODUCTOS DE VENTA").Bold();
                    foreach (var item in sales)
                    {
                        column.Item().Text(item.ProductName).Bold();
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"{item.SaleQuantity} × {Money(item.SoldUnitPrice)}");
                            row.ConstantItem(4);
                            row.AutoItem().Text(Money(item.LineTotal)).Bold();
                        });
                    }
                }
                var hasAdditional = note.Lines.Any(x => x.ReplacementQuantity + x.TastingQuantity > 0);
                if (hasAdditional)
                {
                    if (sales.Count > 0) Line(column);
                    column.Item().Text("ADICIONALES SIN CARGO").Bold();
                    foreach (var item in note.Lines)
                    {
                        Additional(column, item.ProductName, item.ReplacementQuantity, "REPOSICIÓN", item.ReplacementReason, item.ReplacementNotes);
                        Additional(column, item.ProductName, item.TastingQuantity, "DEGUSTACIÓN", item.TastingReason, item.TastingNotes);
                    }
                }
                Line(column);
                if (hasAdditional && note.SaleUnits > 0) Pair(column, "Unidades de venta:", note.SaleUnits.ToString(MoneyCulture));
                if (note.TastingUnits > 0) Pair(column, "Degustación:", note.TastingUnits.ToString(MoneyCulture));
                if (note.ReplacementUnits > 0) Pair(column, "Reposición:", note.ReplacementUnits.ToString(MoneyCulture));
                column.Item().Text($"{(note.Scope == "RegisteredSummary" ? "TOTAL ENTREGADO" : "TOTAL A ENTREGAR")}: {note.PhysicalUnits} unidades").Bold();
                if (note.DeliveryFee > 0)
                {
                    Line(column);
                    Pair(column, "Subtotal productos", Money(note.Subtotal));
                    Pair(column, "Envío", Money(note.DeliveryFee));
                }
                column.Item().BorderTop(.7f).PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text("TOTAL").Bold().FontSize(12);
                    row.AutoItem().Text(Money(note.Total)).Bold().FontSize(12);
                });
                if (note.ShowPaymentStatus && note.SaleUnits > 0 && note.Total > 0 && note.PaymentLabel == "PENDIENTE DE PAGO")
                    column.Item().PaddingTop(3).Border(.6f).PaddingVertical(5).AlignCenter().Text(note.PaymentLabel).Bold().FontSize(9);
                Line(column);
                column.Item().Text("OBSERVACIONES").Bold();
                column.Item().Height(10).BorderBottom(.4f);
                column.Item().Height(10).BorderBottom(.4f);
                column.Item().PaddingTop(3).Text($"Entregado por: {note.Driver}").FontSize(7.5f);
                column.Item().Height(20).BorderBottom(.5f);
                column.Item().AlignCenter().Text("Firma de entrega").FontSize(7);
                column.Item().PaddingTop(3).Text($"Recibido por: {note.Receiver}").FontSize(7.5f);
                column.Item().Height(20).BorderBottom(.5f);
                column.Item().AlignCenter().Text("Firma de recepción").FontSize(7);
                column.Item().PaddingTop(4).Text("La firma acredita la recepción de los productos detallados.").FontSize(7);
                Line(column);
                column.Item().AlignCenter().Text("TENTACIÓN SANA").Bold();
                if (!string.IsNullOrWhiteSpace(note.CommercialPhone)) column.Item().AlignCenter().Text($"Contacto: {note.CommercialPhone}").FontSize(7);
            });
        }));
        var pdf = document.GeneratePdf();
        var dpi = (int)Math.Ceiling(note.PixelWidth * 25.4 / note.PrintableWidthMm);
        var images = document.GenerateImages(new ImageGenerationSettings { RasterDpi = dpi }).ToList();
        if (images.Count != 1) throw new InvalidOperationException("La nota excedió la longitud de una hoja continua. Reduce su contenido.");
        return (pdf, ThermalPng.Monochrome(images[0], note.PixelWidth, note.PaperWidthMm, note.PrintableWidthMm));
    }

    private static void Pair(ColumnDescriptor column, string label, string value) => column.Item().Row(row =>
    {
        row.RelativeItem().Text(label); row.ConstantItem(5); row.AutoItem().Text(value);
    });
    private static void Line(ColumnDescriptor column) => column.Item().PaddingVertical(2).LineHorizontal(.5f).LineColor("#000000");
    private static string[] Words(string? value) => Regex.Matches(value ?? "", @"[\p{L}\p{N}]+").Select(x =>
        string.Concat(x.Value.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToUpperInvariant()).ToArray();

    // Solo se omiten secuencias completas de palabras: nunca coincidencias parciales,
    // ni direcciones que agreguen una calle, número u otro dato diferenciador.
    private static bool Contained(string value, string? previous)
    {
        var words = Words(value); var other = Words(previous);
        return words.Length > 0 && Enumerable.Range(0, Math.Max(0, other.Length - words.Length + 1))
            .Any(start => other.Skip(start).Take(words.Length).SequenceEqual(words));
    }

    private static string? CompactPoint(string customer, string? point)
    {
        if (string.IsNullOrWhiteSpace(point) || Contained(point, customer)) return null;
        var customerWords = Words(customer); var pointWords = Words(point);
        if (customerWords.Length == 0 || pointWords.Length <= customerWords.Length || !pointWords.Take(customerWords.Length).SequenceEqual(customerWords)) return point;
        var prefixEnd = Regex.Matches(point, @"[\p{L}\p{N}]+")[customerWords.Length - 1];
        return point[(prefixEnd.Index + prefixEnd.Length)..].Trim(' ', '-', '–', '—', '·', ',', '/', ':');
    }
    private static void Additional(ColumnDescriptor column, string product, int quantity, string type, string? reason, string? notes)
    {
        if (quantity == 0) return;
        column.Item().PaddingTop(3).Text(type).Bold().FontSize(7.5f);
        column.Item().Text($"{quantity} × {product}");
        if (!string.IsNullOrWhiteSpace(reason)) column.Item().Text($"Motivo: {reason}").FontSize(7.5f);
        if (!string.IsNullOrWhiteSpace(notes)) column.Item().Text($"Obs.: {notes}").FontSize(7.5f);
        column.Item().AlignRight().Text($"Sin cargo: {Money(0)}").FontSize(7.5f);
    }
}
