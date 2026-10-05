using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TentacionSana.Application.Receivables;
using System.Globalization;

namespace TentacionSana.Infrastructure.Receivables;

internal sealed record AccountStatementPdfModel(
    string ReportNumber,
    DateTimeOffset GeneratedAtUtc,
    string GeneratedBy,
    PayerAccountItem Payer,
    IReadOnlyList<ReceivableOrderItem> Orders,
    string ReportType,
    decimal? PreviousBalance,
    decimal? RegisteredPayment,
    decimal CurrentBalance,
    byte[] Logo,
    IReadOnlyList<AccountStatementMovement> Movements,
    IReadOnlyList<AccountStatementEvidence> Evidence);

internal sealed record AccountStatementMovement(DateTimeOffset AtUtc, long OrderNumber, string Method, decimal Amount, decimal BalanceAfter, string? Notes);
internal sealed record AccountStatementEvidence(long OrderNumber, string Type, DateTimeOffset AtUtc, byte[] Content);

internal static class AccountStatementPdfDocument
{
    private const string Ink = "#111B35";
    private const string Green = "#168A3B";
    private const string Lime = "#B8EA62";
    private const string Paper = "#F6F4EE";
    private const string Line = "#DDE2DC";
    private const string Muted = "#667085";
    private const string Danger = "#D92D20";

    public static byte[] Generate(AccountStatementPdfModel model)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = false;
        return BuildDocument(model).GeneratePdf();
    }

    internal static IReadOnlyList<byte[]> GeneratePreviewImages(AccountStatementPdfModel model)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = false;
        return BuildDocument(model).GenerateImages(new ImageGenerationSettings { RasterDpi = 120 }).ToList();
    }

    private static Document BuildDocument(AccountStatementPdfModel model) =>
        Document.Create(document =>
        {
            document.Page(page => ComposeSummary(page, model));
            if (model.ReportType == "History" && model.Movements.Count > 0) document.Page(page => ComposeMovements(page, model));
            foreach (var evidence in model.Evidence) document.Page(page => ComposeEvidence(page, model, evidence));
        });

    private static void ComposeSummary(PageDescriptor page, AccountStatementPdfModel model)
    {
        page.Size(PageSizes.A4);
        page.Margin(34);
        page.DefaultTextStyle(x => x.FontFamily("Lato").FontSize(9).FontColor(Ink));
        page.Header().Row(row =>
        {
            row.ConstantItem(118).Height(56).Background(Ink).Padding(8).Element(container => AddLogo(container, model.Logo, true));
            row.RelativeItem().PaddingLeft(18).Column(column =>
            {
                column.Item().Text("ESTADO DE CUENTA").FontSize(22).SemiBold().FontColor(Ink);
                column.Item().PaddingTop(3).Text(model.ReportType == "History" ? "Historial de cuenta y movimientos" : "Saldos exigibles actuales").FontColor(Muted);
            });
            row.ConstantItem(135).AlignRight().Column(column =>
            {
                column.Item().Text(model.ReportNumber).SemiBold().FontColor(Green);
                column.Item().PaddingTop(3).Text(model.GeneratedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture));
                column.Item().Text($"Generado por {model.GeneratedBy}").FontSize(8).FontColor(Muted);
            });
        });
        page.Content().PaddingTop(24).Column(column =>
        {
            column.Spacing(14);
            column.Item().Border(1).BorderColor(Line).Background(Paper).Padding(14).Row(row =>
            {
                row.RelativeItem(2).Column(info =>
                {
                    info.Item().Text("RESPONSABLE DE PAGO").FontSize(8).SemiBold().FontColor(Green).LetterSpacing(.08f);
                    info.Item().PaddingTop(4).Text(model.Payer.Name).FontSize(15).SemiBold();
                    if (!string.IsNullOrWhiteSpace(model.Payer.ContactName)) info.Item().Text(model.Payer.ContactName!).FontColor(Muted);
                    if (!string.IsNullOrWhiteSpace(model.Payer.Phone)) info.Item().Text(model.Payer.Phone).FontColor(Muted);
                });
                row.RelativeItem().Column(info =>
                {
                    info.Item().Text("CLIENTE / CLIENTES").FontSize(8).SemiBold().FontColor(Green).LetterSpacing(.08f);
                    foreach (var customer in model.Payer.Customers) info.Item().PaddingTop(4).Text(customer);
                });
            });

            if (model.RegisteredPayment is not null)
            {
                column.Item().Row(row =>
                {
                    Metric(row.RelativeItem(), "Saldo anterior", model.PreviousBalance ?? 0, Ink);
                    row.ConstantItem(10);
                    Metric(row.RelativeItem(), "Pago registrado", model.RegisteredPayment.Value, Green);
                    row.ConstantItem(10);
                    Metric(row.RelativeItem(), "Saldo posterior", model.CurrentBalance, model.CurrentBalance > 0 ? Danger : Green);
                });
            }

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(48); columns.RelativeColumn(1.4f); columns.ConstantColumn(66);
                    columns.ConstantColumn(60); columns.ConstantColumn(60); columns.ConstantColumn(60); columns.ConstantColumn(53);
                });
                table.Header(header =>
                {
                    HeaderCell(header.Cell(), "Pedido"); HeaderCell(header.Cell(), "Sucursal"); HeaderCell(header.Cell(), "Entrega");
                    HeaderCell(header.Cell(), "Total"); HeaderCell(header.Cell(), "Pagado"); HeaderCell(header.Cell(), "Saldo"); HeaderCell(header.Cell(), "Estado");
                });
                foreach (var order in model.Orders)
                {
                    BodyCell(table.Cell(), $"#{order.OrderNumber}", true);
                    BodyCell(table.Cell(), order.Branch);
                    BodyCell(table.Cell(), order.DeliveredAtUtc?.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Sin fecha");
                    BodyCell(table.Cell(), Money(order.Total)); BodyCell(table.Cell(), Money(order.Paid));
                    BodyCell(table.Cell(), Money(order.Balance), order.Balance > 0);
                    BodyCell(table.Cell(), StatusText(order.Status));
                }
                TotalCell(table.Cell().ColumnSpan(3), "TOTALES");
                TotalCell(table.Cell(), Money(model.Orders.Sum(x => x.Total)));
                TotalCell(table.Cell(), Money(model.Orders.Sum(x => x.Paid)));
                TotalCell(table.Cell(), Money(model.Orders.Sum(x => x.Balance)), true);
                TotalCell(table.Cell(), string.Empty);
            });

            column.Item().AlignRight().Width(275).BorderTop(2).BorderColor(Ink).PaddingTop(10).Column(totals =>
            {
                TotalsRow(totals, "Total exigible", model.Orders.Sum(x => x.Total));
                TotalsRow(totals, "Total pagado", model.Orders.Sum(x => x.Paid));
                totals.Item().PaddingTop(6).Row(row =>
                {
                    row.RelativeItem().Text("SALDO PENDIENTE").FontSize(10).SemiBold();
                    row.ConstantItem(105).AlignRight().Text(Money(model.Orders.Sum(x => x.Balance))).FontSize(14).Bold().FontColor(model.CurrentBalance > 0 ? Danger : Green);
                });
            });
        });
        page.Footer().BorderTop(1).BorderColor(Line).PaddingTop(8).Row(row =>
        {
            row.RelativeItem().Text("Tentaci\u00F3n Sana | Delicioso - saludable - premium").FontSize(8).FontColor(Muted);
            row.RelativeItem().AlignRight().Text(text => { text.Span("P\u00E1gina "); text.CurrentPageNumber(); text.Span(" de "); text.TotalPages(); });
        });
    }

    private static void ComposeMovements(PageDescriptor page, AccountStatementPdfModel model)
    {
        page.Size(PageSizes.A4);
        page.Margin(34);
        page.DefaultTextStyle(x => x.FontFamily("Lato").FontSize(9).FontColor(Ink));
        page.Header().Height(72).BorderBottom(1).BorderColor(Line).Row(row =>
        {
            row.ConstantItem(104).PaddingBottom(12).Element(container => AddLogo(container, model.Logo, false));
            row.RelativeItem().PaddingLeft(18).Column(column =>
            {
                column.Item().Text("PAGOS Y MOVIMIENTOS").FontSize(16).Bold();
                column.Item().Text("Aplicaciones hist\u00F3ricas y saldo resultante").FontColor(Muted);
            });
            row.ConstantItem(135).AlignRight().Text(model.ReportNumber).FontSize(8).FontColor(Muted);
        });
        page.Content().PaddingTop(18).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(78); columns.ConstantColumn(58); columns.ConstantColumn(64);
                columns.ConstantColumn(78); columns.ConstantColumn(88); columns.RelativeColumn();
            });
            table.Header(header =>
            {
                HeaderCell(header.Cell(), "Fecha"); HeaderCell(header.Cell(), "Pedido"); HeaderCell(header.Cell(), "M\u00E9todo");
                HeaderCell(header.Cell(), "Pago aplicado"); HeaderCell(header.Cell(), "Saldo resultante"); HeaderCell(header.Cell(), "Observaci\u00F3n");
            });
            foreach (var movement in model.Movements)
            {
                BodyCell(table.Cell(), movement.AtUtc.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
                BodyCell(table.Cell(), $"#{movement.OrderNumber}", true); BodyCell(table.Cell(), movement.Method == "Qr" ? "QR" : "Efectivo");
                BodyCell(table.Cell(), Money(movement.Amount)); BodyCell(table.Cell(), Money(movement.BalanceAfter), movement.BalanceAfter > 0);
                BodyCell(table.Cell(), movement.Notes ?? "-");
            }
        });
        page.Footer().BorderTop(1).BorderColor(Line).PaddingTop(8).Row(row =>
        {
            row.RelativeItem().Text("Historial de pagos registrado en Tentaci\u00F3n Sana").FontSize(8).FontColor(Muted);
            row.RelativeItem().AlignRight().Text(text => { text.Span("P\u00E1gina "); text.CurrentPageNumber(); text.Span(" de "); text.TotalPages(); });
        });
    }

    private static void ComposeEvidence(PageDescriptor page, AccountStatementPdfModel model, AccountStatementEvidence evidence)
    {
        page.Size(PageSizes.A4);
        page.Margin(34);
        page.DefaultTextStyle(x => x.FontFamily("Lato").FontSize(9).FontColor(Ink));
        page.Header().Height(72).BorderBottom(1).BorderColor(Line).Row(row =>
        {
            row.ConstantItem(104).PaddingBottom(12).Element(container => AddLogo(container, model.Logo, false));
            row.RelativeItem().PaddingLeft(18).Column(column =>
            {
                column.Item().Text($"PEDIDO #{evidence.OrderNumber}").FontSize(15).Bold();
                column.Item().PaddingTop(2).Text(evidence.Type).FontSize(11).SemiBold().FontColor(Green);
                column.Item().Text(evidence.AtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)).FontColor(Muted);
            });
            row.ConstantItem(135).AlignRight().Text(model.ReportNumber).FontSize(8).FontColor(Muted);
        });
        page.Content().PaddingTop(18).PaddingBottom(10).Border(1).BorderColor(Line).Background(Paper).Padding(12)
            .AlignCenter().AlignMiddle().Image(evidence.Content).FitArea();
        page.Footer().BorderTop(1).BorderColor(Line).PaddingTop(8).Row(row =>
        {
            row.RelativeItem().Text("Evidencia incluida en el estado de cuenta").FontSize(8).FontColor(Muted);
            row.RelativeItem().AlignRight().Text(text => { text.Span("P\u00E1gina "); text.CurrentPageNumber(); text.Span(" de "); text.TotalPages(); });
        });
    }

    private static void AddLogo(IContainer container, byte[] logo, bool onDark)
    {
        if (logo.Length > 0) container.AlignCenter().AlignMiddle().Image(logo).FitArea();
        else container.AlignCenter().AlignMiddle().Text("TENTACI\u00D3N SANA").Bold().FontColor(onDark ? Colors.White : Ink);
    }

    private static void Metric(IContainer container, string label, decimal amount, string color) =>
        container.Border(1).BorderColor(Line).Padding(12).Column(column =>
        {
            column.Item().Text(label).FontSize(8).FontColor(Muted);
            column.Item().PaddingTop(3).Text(Money(amount)).FontSize(14).Bold().FontColor(color);
        });

    private static void HeaderCell(IContainer cell, string value) => cell.Background(Ink).PaddingVertical(7).PaddingHorizontal(5).Text(value).FontSize(7).SemiBold().FontColor(Colors.White);
    private static void BodyCell(IContainer cell, string value, bool emphasis = false)
    {
        var text = cell.BorderBottom(1).BorderColor(Line).PaddingVertical(7).PaddingHorizontal(5).Text(value).FontSize(7).FontColor(emphasis ? Danger : Ink);
        if (emphasis) text.SemiBold();
    }
    private static void TotalCell(IContainer cell, string value, bool emphasis = false) => cell.Background(Paper).PaddingVertical(7).PaddingHorizontal(5).Text(value).FontSize(7).FontColor(emphasis ? Danger : Ink).SemiBold();
    private static void TotalsRow(ColumnDescriptor column, string label, decimal amount) => column.Item().PaddingVertical(2).Row(row => { row.RelativeItem().Text(label).FontColor(Muted); row.ConstantItem(105).AlignRight().Text(Money(amount)).SemiBold(); });
    private static string Money(decimal value) => $"Bs {value:N2}";
    private static string StatusText(string status) => status switch { "Paid" => "Pagado", "Partial" => "Parcial", "Overdue" => "Vencido", _ => "Sin pago" };
}
