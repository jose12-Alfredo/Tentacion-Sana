namespace TentacionSana.Domain.Deliveries;

public sealed class DeliveryEvidence
{
    private DeliveryEvidence() { }
    public Guid Id { get; private set; }
    public Guid DeliveryId { get; private set; }
    public string PublicId { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string Format { get; private set; } = string.Empty;
    public long Bytes { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public DateTimeOffset UploadedAtUtc { get; private set; }
    public bool IsActive { get; private set; }

    public static DeliveryEvidence Create(Guid deliveryId, string publicId, string fileName, string format, long bytes, Guid userId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(publicId)) throw new ArgumentException("Cloudinary no devolvió un identificador válido.");
        return new DeliveryEvidence
        {
            Id = Guid.NewGuid(), DeliveryId = deliveryId, PublicId = publicId,
            FileName = Path.GetFileName(fileName), Format = format, Bytes = bytes,
            UploadedByUserId = userId, UploadedAtUtc = now, IsActive = true
        };
    }
}
