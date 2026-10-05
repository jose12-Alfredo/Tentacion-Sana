using System.Text.Json;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TentacionSana.Application.Deliveries;
using TentacionSana.Domain.Deliveries;
using TentacionSana.Infrastructure.Auditing;
using TentacionSana.Infrastructure.Persistence;

namespace TentacionSana.Infrastructure.Media;

public sealed class CloudinaryDeliveryEvidenceService(
    ApplicationDbContext db,
    IOptions<CloudinaryOptions> options,
    TimeProvider clock,
    IHttpClientFactory httpClientFactory) : IDeliveryEvidenceService
{
    private const long MaximumBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedTypes = ["image/jpeg", "image/png", "image/webp"];

    public async Task<EvidenceResult> UploadAsync(UploadEvidenceCommand command, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!AllowedTypes.Contains(command.ContentType.ToLowerInvariant())) return new(false, null, ["La evidencia debe ser JPG, PNG o WebP."]);
        if (command.Length <= 0 || command.Length > MaximumBytes) return new(false, null, ["La evidencia debe pesar como máximo 10 MB."]);
        if (!await db.Deliveries.AnyAsync(x => x.Id == command.DeliveryId, cancellationToken)) return new(false, null, ["La entrega no existe."]);
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.CloudName) || string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.ApiSecret)) return new(false, null, ["Cloudinary no está configurado."]);

        var cloudinary = CreateClient(settings);
        var upload = await cloudinary.UploadAsync(new ImageUploadParams
        {
            File = new FileDescription(Path.GetFileName(command.FileName), command.Content),
            Folder = $"tentacion-sana/entregas/{command.DeliveryId:N}",
            Type = "authenticated",
            UniqueFilename = true,
            Overwrite = false,
            UseFilename = true
        }, cancellationToken);
        if (upload.Error is not null || string.IsNullOrWhiteSpace(upload.PublicId)) return new(false, null, [upload.Error?.Message ?? "Cloudinary no devolvió una evidencia válida."]);

        var now = clock.GetUtcNow();
        var evidence = DeliveryEvidence.Create(command.DeliveryId, upload.PublicId, command.FileName, upload.Format ?? string.Empty, upload.Bytes, userId, now);
        db.DeliveryEvidence.Add(evidence);
        db.AuditEntries.Add(new AuditEntry { Id = Guid.NewGuid(), UserId = userId, Action = "DeliveryEvidenceUploaded", EntityType = nameof(DeliveryEvidence), EntityId = evidence.Id.ToString(), NewValuesJson = JsonSerializer.Serialize(new { evidence.DeliveryId, evidence.PublicId, evidence.Bytes }), OccurredAtUtc = now });
        await db.SaveChangesAsync(cancellationToken);
        return new(true, evidence.Id, []);
    }

    public async Task<SignedEvidenceResult> GetSignedUrlAsync(Guid evidenceId, Guid userId, CancellationToken cancellationToken = default)
    {
        var exists = await db.DeliveryEvidence.AsNoTracking().AnyAsync(x => x.Id == evidenceId && x.IsActive, cancellationToken);
        if (!exists) return new(false, null, ["La evidencia no existe."]);
        return new(true, $"/media/delivery-evidence/{evidenceId:N}", []);
    }

    public async Task<EvidenceContentResult> GetContentAsync(Guid evidenceId, Guid userId, CancellationToken cancellationToken = default)
    {
        var evidence = await db.DeliveryEvidence.AsNoTracking().SingleOrDefaultAsync(x => x.Id == evidenceId && x.IsActive, cancellationToken);
        if (evidence is null) return new(false, null, null, ["La evidencia no existe."]);
        var downloaded = await CloudinaryPrivateMedia.DownloadAsync(options.Value, httpClientFactory,
            evidence.PublicId, evidence.Format, evidence.FileName, clock.GetUtcNow().AddMinutes(5), cancellationToken);
        if (downloaded is null) return new(false, null, null, ["No se pudo recuperar la evidencia."]);
        db.AuditEntries.Add(new AuditEntry { Id = Guid.NewGuid(), UserId = userId, Action = "DeliveryEvidenceViewed", EntityType = nameof(DeliveryEvidence), EntityId = evidence.Id.ToString(), OccurredAtUtc = clock.GetUtcNow() });
        await db.SaveChangesAsync(cancellationToken);
        return new(true, downloaded.Value.Content, downloaded.Value.ContentType, []);
    }

    private static Cloudinary CreateClient(CloudinaryOptions settings) => new(new Account(settings.CloudName, settings.ApiKey, settings.ApiSecret)) { Api = { Secure = true } };
}
