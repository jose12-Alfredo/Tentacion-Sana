using System.Text.Json;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TentacionSana.Application.Catalog;
using TentacionSana.Domain.Catalog;
using TentacionSana.Infrastructure.Auditing;
using TentacionSana.Infrastructure.Persistence;

namespace TentacionSana.Infrastructure.Media;

public sealed class CloudinaryProductImageService(
    ApplicationDbContext dbContext,
    IOptions<CloudinaryOptions> options,
    TimeProvider timeProvider) : IProductImageManagementService
{
    private const long MaximumBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedContentTypes =
        ["image/jpeg", "image/png", "image/webp"];

    public async Task<UploadProductImageResult> UploadAsync(
        UploadProductImageRequest request,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return new(false, errors);
        }

        if (!await dbContext.Products.AnyAsync(product => product.Id == request.ProductId, cancellationToken))
        {
            return new(false, ["El producto ya no existe."]);
        }

        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.CloudName)
            || string.IsNullOrWhiteSpace(settings.ApiKey)
            || string.IsNullOrWhiteSpace(settings.ApiSecret))
        {
            return new(false, ["Cloudinary aún no está configurado en este ambiente."]);
        }

        var cloudinary = new Cloudinary(new Account(settings.CloudName, settings.ApiKey, settings.ApiSecret))
        {
            Api = { Secure = true }
        };
        var upload = await cloudinary.UploadAsync(new ImageUploadParams
        {
            File = new FileDescription(request.FileName, request.Content),
            Folder = $"tentacion-sana/products/{request.ProductId:N}",
            UniqueFilename = true,
            Overwrite = false,
            UseFilename = true
        }, cancellationToken);

        if (upload.Error is not null || string.IsNullOrWhiteSpace(upload.PublicId) || upload.SecureUrl is null)
        {
            return new(false, [upload.Error?.Message ?? "Cloudinary no devolvió una imagen válida."]);
        }

        var existingImages = await dbContext.ProductImages
            .Where(image => image.ProductId == request.ProductId)
            .ToListAsync(cancellationToken);
        foreach (var existingImage in existingImages)
        {
            existingImage.SetAsSecondary();
        }
        var now = timeProvider.GetUtcNow();
        var image = new ProductImage(
            Guid.NewGuid(), request.ProductId, upload.PublicId, upload.SecureUrl.AbsoluteUri,
            upload.Format ?? string.Empty, upload.Width, upload.Height, upload.Bytes,
            request.AltText, true, 0, now);

        dbContext.ProductImages.Add(image);
        dbContext.AuditEntries.Add(new AuditEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = "ProductImageUploaded",
            EntityType = nameof(ProductImage),
            EntityId = image.Id.ToString(),
            NewValuesJson = JsonSerializer.Serialize(new { image.ProductId, image.PublicId, image.Width, image.Height }),
            OccurredAtUtc = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(true, []);
    }

    private static List<string> Validate(UploadProductImageRequest request)
    {
        var errors = new List<string>();
        if (!AllowedContentTypes.Contains(request.ContentType.ToLowerInvariant()))
            errors.Add("La imagen debe ser JPG, PNG o WebP.");
        if (request.Length <= 0 || request.Length > MaximumBytes)
            errors.Add("La imagen debe pesar como máximo 10 MB.");
        if (string.IsNullOrWhiteSpace(request.AltText))
            errors.Add("El texto alternativo es obligatorio.");
        return errors;
    }
}
