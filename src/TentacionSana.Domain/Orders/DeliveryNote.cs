namespace TentacionSana.Domain.Orders;

// Documento emitido: sus datos y archivos son inmutables, incluso si cambia el pedido.
public sealed class DeliveryNote
{
    private DeliveryNote() { }
    public Guid Id { get; private set; }
    public long Number { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid? DeliveryId { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid IssuedByUserId { get; private set; }
    public DateTimeOffset IssuedAtUtc { get; private set; }
    public string Scope { get; private set; } = "";
    public string Fingerprint { get; private set; } = "";
    public string SnapshotJson { get; private set; } = "";
    public string TemplateVersion { get; private set; } = "58mm-v1";
    public byte[] Pdf { get; private set; } = [];
    public byte[] Png { get; private set; } = [];
    public static DeliveryNote Create(long number, Guid orderId, Guid? deliveryId, Guid requestId, Guid userId,
        DateTimeOffset now, string scope, string fingerprint, string snapshot, byte[] pdf, byte[] png)
    {
        if (number < 1 || orderId == Guid.Empty || requestId == Guid.Empty || userId == Guid.Empty ||
            fingerprint.Length != 64 || string.IsNullOrWhiteSpace(snapshot) || pdf.Length == 0 || png.Length == 0)
            throw new ArgumentException("La nota emitida requiere identificación, contenido y ambos archivos.");
        return new() { Id = Guid.NewGuid(), Number = number, OrderId = orderId, DeliveryId = deliveryId, RequestId = requestId,
            IssuedByUserId = userId, IssuedAtUtc = now, Scope = scope, Fingerprint = fingerprint, SnapshotJson = snapshot,
            TemplateVersion = "58mm-v2.2", Pdf = pdf.ToArray(), Png = png.ToArray() };
    }
}
