using System.Text.Json;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TentacionSana.Application.Customers;
using TentacionSana.Infrastructure.Auditing;
using TentacionSana.Infrastructure.Persistence;

#pragma warning disable CA1725

namespace TentacionSana.Infrastructure.Media;

public sealed class CloudinaryDeliveryPointImageService(
    ApplicationDbContext db,
    IOptions<CloudinaryOptions> options,
    TimeProvider clock,
    IHttpClientFactory httpClientFactory) : IDeliveryPointImageService
{
    private const long MaximumBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedTypes = ["image/jpeg", "image/png", "image/webp"];

    public async Task<CustomerOperationResult> UploadAsync(Guid customerId, Guid pointId, Stream content, string fileName, string contentType, long length, Guid userId, CancellationToken ct = default)
    {
        if (!AllowedTypes.Contains(contentType.ToLowerInvariant())) return Fail("La imagen debe ser JPG, PNG o WebP.");
        if (length <= 0 || length > MaximumBytes) return Fail("La imagen debe pesar como máximo 10 MB.");
        var point = await db.DeliveryPoints.SingleOrDefaultAsync(x => x.Id == pointId && x.CustomerId == customerId, ct);
        if (point is null) return Fail("El punto no existe.");
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.CloudName) || string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.ApiSecret)) return Fail("Cloudinary no está configurado.");
        var cloudinary = Client(settings);
        var upload = await cloudinary.UploadAsync(new ImageUploadParams { File = new FileDescription(Path.GetFileName(fileName), content), Folder = $"tentacion-sana/clientes/{customerId:N}/puntos/{pointId:N}", Type = "authenticated", UniqueFilename = true, Overwrite = false, UseFilename = true }, ct);
        if (upload.Error is not null || string.IsNullOrWhiteSpace(upload.PublicId)) return Fail(upload.Error?.Message ?? "No se pudo guardar la imagen.");
        point.SetImage(upload.PublicId, upload.Format ?? "", fileName);
        db.AuditEntries.Add(new AuditEntry { Id = Guid.NewGuid(), UserId = userId, Action = "DeliveryPointImageUploaded", EntityType = "DeliveryPoint", EntityId = pointId.ToString(), NewValuesJson = JsonSerializer.Serialize(new { upload.PublicId, upload.Bytes }), OccurredAtUtc = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct); return new(true, pointId, []);
    }

    public async Task<string?> GetUrlAsync(Guid pointId, CancellationToken ct = default)
    {
        var exists = await db.DeliveryPoints.AsNoTracking()
            .AnyAsync(x => x.Id == pointId && x.ImagePublicId != null, ct);
        return exists ? $"/media/delivery-points/{pointId:N}" : null;
    }

    public async Task<DeliveryPointImageContent?> GetContentAsync(Guid pointId, CancellationToken ct = default)
    {
        var image = await db.DeliveryPoints.AsNoTracking().Where(x => x.Id == pointId)
            .Select(x => new { x.ImagePublicId, x.ImageFormat, x.ImageFileName }).SingleOrDefaultAsync(ct);
        if (image?.ImagePublicId is null) return null;

        var downloaded = await CloudinaryPrivateMedia.DownloadAsync(options.Value, httpClientFactory,
            image.ImagePublicId, image.ImageFormat ?? string.Empty, image.ImageFileName,
            clock.GetUtcNow().AddMinutes(5), ct);
        return downloaded is null ? null : new(downloaded.Value.Content, downloaded.Value.ContentType);
    }

    private static Cloudinary Client(CloudinaryOptions s) => new(new Account(s.CloudName, s.ApiKey, s.ApiSecret)) { Api = { Secure = true } };
    private static CustomerOperationResult Fail(string error) => new(false, null, [error]);
}
