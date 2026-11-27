namespace TentacionSana.Application.Orders;

public interface IDeliveryNoteService
{
    Task<NoteResult<DeliveryNoteContext>> GetContextAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default);
    Task<NoteResult<DeliveryNotePreview>> PreviewAsync(Guid orderId, Guid? deliveryId, Guid userId, CancellationToken cancellationToken = default);
    Task<NoteResult<DeliveryNotePreview>> PreviewAsync(Guid orderId, Guid? deliveryId, Guid userId, bool? showPaymentStatus, CancellationToken cancellationToken = default);
    Task<NoteResult<DeliveryNoteFiles>> IssueAsync(IssueDeliveryNote command, Guid userId, CancellationToken cancellationToken = default);
    Task<NoteResult<DeliveryNoteFiles>> GetIssuedAsync(Guid noteId, Guid userId, CancellationToken cancellationToken = default);
}
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000", Justification = "Factorías tipadas para resultados documentales sin excepciones de validación.")]
public sealed record NoteResult<T>(bool Succeeded, T? Value, IReadOnlyList<string> Errors)
{
    public static NoteResult<T> Ok(T value) => new(true, value, []);
    public static NoteResult<T> Fail(string error) => new(false, default, [error]);
}
public sealed record DeliveryNoteScope(Guid? DeliveryId, string Label);
public sealed record DeliveryNoteHistory(Guid Id, string Number, DateTimeOffset IssuedAtUtc, string Scope);
public sealed record DeliveryNoteContext(IReadOnlyList<DeliveryNoteScope> Scopes, IReadOnlyList<DeliveryNoteHistory> Issued);
public sealed record IssueDeliveryNote(Guid OrderId, Guid? DeliveryId, string Fingerprint, Guid RequestId, bool ShowPaymentStatus = false);
public sealed record DeliveryNotePreview(ThermalNoteContent Content, string Fingerprint, byte[] Pdf, byte[] Png, IReadOnlyList<string> Warnings);
public sealed record DeliveryNoteFiles(Guid Id, string Number, ThermalNoteContent Content, byte[] Pdf, byte[] Png);
public sealed record ThermalNoteLine(Guid OrderLineId, string ProductName, int SaleQuantity, int ReplacementQuantity, int TastingQuantity,
    decimal SoldUnitPrice, string? ReplacementReason, string? ReplacementNotes, string? TastingReason, string? TastingNotes)
{
    public decimal LineTotal => decimal.Round(SaleQuantity * SoldUnitPrice, 2, MidpointRounding.AwayFromZero);
    public int PhysicalQuantity => SaleQuantity + ReplacementQuantity + TastingQuantity;
}
public sealed record ThermalNoteContent(Guid OrderId, long OrderNumber, Guid? DeliveryId, string Scope, string SourceVersion,
    DateTimeOffset IssuedAtUtc, string Customer, string? Point, string? Address, string? ContactPhone,
    string? Driver, string? Receiver, DateTimeOffset? ScheduledAtUtc, DateTimeOffset? CompletedAtUtc,
    IReadOnlyList<ThermalNoteLine> Lines, decimal DeliveryFee, string? CommercialPhone, int PixelWidth,
    float PaperWidthMm, float PrintableWidthMm, byte[] Logo)
{
    public int SaleUnits => Lines.Sum(x => x.SaleQuantity);
    public int ReplacementUnits => Lines.Sum(x => x.ReplacementQuantity);
    public int TastingUnits => Lines.Sum(x => x.TastingQuantity);
    public int PhysicalUnits => Lines.Sum(x => x.PhysicalQuantity);
    public decimal Subtotal => Lines.Sum(x => x.LineTotal);
    public decimal Total => Subtotal + DeliveryFee;
    public string TemplateVersion { get; init; } = "58mm-v1";
    public bool ShowPaymentStatus { get; init; }
    public string? PaymentLabel { get; init; }
    public string? PaymentWarning { get; init; }
}
public sealed class ThermalNoteOptions
{
    public const string Section = "ThermalNotes";
    public float PaperWidthMm { get; set; } = 58;
    public float PrintableWidthMm { get; set; } = 48;
    public int PixelWidth { get; set; } = 384;
    public string LogoPath { get; set; } = "wwwroot/images/brand/logo_tentacion_sana_termico.png";
    public string ContactPhone { get; set; } = "78969873";
}
