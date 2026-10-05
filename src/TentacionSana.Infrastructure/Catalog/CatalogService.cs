using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TentacionSana.Application.Catalog;
using TentacionSana.Domain.Catalog;
using TentacionSana.Infrastructure.Auditing;
using TentacionSana.Infrastructure.Persistence;

namespace TentacionSana.Infrastructure.Catalog;

public sealed class CatalogService(ApplicationDbContext dbContext, TimeProvider timeProvider)
    : IPublicCatalogService, IProductManagementService
{
    public async Task<IReadOnlyList<PublicProductSummary>> GetPublishedProductsAsync(
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        return await (
            from product in dbContext.Products.AsNoTracking()
            join publication in dbContext.ProductPublications.AsNoTracking()
                on product.Id equals publication.ProductId
            where product.IsActive && publication.IsPublished
            orderby publication.IsFeatured descending, publication.DisplayOrder, product.Name
            select new PublicProductSummary(
                product.Id,
                product.Name,
                product.Slug,
                product.Presentation,
                publication.PublicDescription,
                publication.ShowPublicPrice
                    ? dbContext.ProductPrices
                        .Where(price => price.ProductId == product.Id
                            && price.EffectiveFromUtc <= now
                            && (price.EffectiveToUtc == null || price.EffectiveToUtc > now))
                        .OrderByDescending(price => price.EffectiveFromUtc)
                        .Select(price => (decimal?)price.Amount)
                        .FirstOrDefault()
                    : null,
                dbContext.ProductImages
                    .Where(image => image.ProductId == product.Id)
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.DisplayOrder)
                    .Select(image => image.SecureUrl)
                    .FirstOrDefault(),
                dbContext.ProductImages
                    .Where(image => image.ProductId == product.Id)
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.DisplayOrder)
                    .Select(image => image.AltText)
                    .FirstOrDefault(),
                publication.IsFeatured))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LandingProductSlide>> GetLandingProductsAsync(
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        return await (
            from product in dbContext.Products.AsNoTracking()
            join publication in dbContext.ProductPublications.AsNoTracking()
                on product.Id equals publication.ProductId
            let image = dbContext.ProductImages
                .Where(item => item.ProductId == product.Id)
                .OrderByDescending(item => item.IsPrimary)
                .ThenBy(item => item.DisplayOrder)
                .Select(item => new { item.SecureUrl, item.AltText, item.Width, item.Height })
                .FirstOrDefault()
            where product.IsActive
                && publication.IsPublished
                && publication.IsLandingFeatured
                && image != null
                && image.SecureUrl != ""
            orderby publication.LandingOrder, product.Name
            select new LandingProductSlide(
                product.Id,
                product.Name,
                product.Slug,
                product.Presentation,
                publication.PublicDescription,
                publication.ApprovedBenefits,
                publication.ShowPublicPrice
                    ? dbContext.ProductPrices
                        .Where(price => price.ProductId == product.Id
                            && price.EffectiveFromUtc <= now
                            && (price.EffectiveToUtc == null || price.EffectiveToUtc > now))
                        .OrderByDescending(price => price.EffectiveFromUtc)
                        .Select(price => (decimal?)price.Amount)
                        .FirstOrDefault()
                    : null,
                image!.SecureUrl,
                image.AltText,
                image.Width,
                image.Height,
                publication.LandingTitle,
                publication.LandingSubtitle,
                publication.LandingBadgeText,
                publication.LandingOrder))
            .ToListAsync(cancellationToken);
    }

    public async Task<PublicProductDetail?> GetPublishedProductAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var now = timeProvider.GetUtcNow();
        var detail = await (
            from product in dbContext.Products.AsNoTracking()
            join publication in dbContext.ProductPublications.AsNoTracking()
                on product.Id equals publication.ProductId
            where product.Slug == normalizedSlug && product.IsActive && publication.IsPublished
            select new
            {
                product.Id,
                product.Name,
                product.Slug,
                product.Presentation,
                publication.PublicDescription,
                publication.ApprovedBenefits,
                Price = publication.ShowPublicPrice
                    ? dbContext.ProductPrices
                        .Where(price => price.ProductId == product.Id
                            && price.EffectiveFromUtc <= now
                            && (price.EffectiveToUtc == null || price.EffectiveToUtc > now))
                        .OrderByDescending(price => price.EffectiveFromUtc)
                        .Select(price => (decimal?)price.Amount)
                        .FirstOrDefault()
                    : null
            }).SingleOrDefaultAsync(cancellationToken);

        if (detail is null)
        {
            return null;
        }

        var images = await dbContext.ProductImages.AsNoTracking()
            .Where(image => image.ProductId == detail.Id)
            .OrderByDescending(image => image.IsPrimary)
            .ThenBy(image => image.DisplayOrder)
            .Select(image => new PublicProductImage(image.SecureUrl, image.AltText, image.Width, image.Height))
            .ToListAsync(cancellationToken);

        return new PublicProductDetail(
            detail.Id,
            detail.Name,
            detail.Slug,
            detail.Presentation,
            detail.PublicDescription,
            detail.ApprovedBenefits,
            detail.Price,
            images);
    }

    public async Task<IReadOnlyList<ManagedProductSummary>> GetProductsAsync(
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        return await dbContext.Products.AsNoTracking()
            .OrderBy(product => product.Name)
            .Select(product => new ManagedProductSummary(
                product.Id,
                product.Name,
                product.Slug,
                product.Presentation,
                dbContext.ProductPrices
                    .Where(price => price.ProductId == product.Id
                        && price.EffectiveFromUtc <= now
                        && (price.EffectiveToUtc == null || price.EffectiveToUtc > now))
                    .OrderByDescending(price => price.EffectiveFromUtc)
                    .Select(price => price.Amount)
                    .FirstOrDefault(),
                product.IsActive,
                dbContext.ProductPublications
                    .Where(publication => publication.ProductId == product.Id)
                    .Select(publication => publication.IsPublished)
                    .FirstOrDefault(),
                dbContext.ProductPublications
                    .Where(publication => publication.ProductId == product.Id)
                    .Select(publication => publication.IsFeatured)
                    .FirstOrDefault(),
                dbContext.ProductPublications
                    .Where(publication => publication.ProductId == product.Id)
                    .Select(publication => publication.PublicDescription)
                    .FirstOrDefault() ?? string.Empty,
                dbContext.ProductPublications
                    .Where(publication => publication.ProductId == product.Id)
                    .Select(publication => publication.ShowPublicPrice)
                    .FirstOrDefault(),
                dbContext.ProductPublications
                    .Where(publication => publication.ProductId == product.Id)
                    .Select(publication => publication.ApprovedBenefits)
                    .FirstOrDefault(),
                dbContext.ProductPublications
                    .Where(publication => publication.ProductId == product.Id)
                    .Select(publication => publication.IsLandingFeatured)
                    .FirstOrDefault(),
                dbContext.ProductPublications
                    .Where(publication => publication.ProductId == product.Id)
                    .Select(publication => publication.LandingOrder)
                    .FirstOrDefault(),
                dbContext.ProductPublications
                    .Where(publication => publication.ProductId == product.Id)
                    .Select(publication => publication.LandingTitle)
                    .FirstOrDefault(),
                dbContext.ProductPublications
                    .Where(publication => publication.ProductId == product.Id)
                    .Select(publication => publication.LandingSubtitle)
                    .FirstOrDefault(),
                dbContext.ProductPublications
                    .Where(publication => publication.ProductId == product.Id)
                    .Select(publication => publication.LandingBadgeText)
                    .FirstOrDefault(),
                dbContext.ProductCategories
                    .Where(category => category.Id == product.CategoryId)
                    .Select(category => category.Name)
                    .FirstOrDefault(),
                dbContext.ProductImages
                    .Where(image => image.ProductId == product.Id)
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.DisplayOrder)
                    .Select(image => image.SecureUrl)
                    .FirstOrDefault(),
                dbContext.ProductImages
                    .Where(image => image.ProductId == product.Id)
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.DisplayOrder)
                    .Select(image => image.AltText)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductStateResult> SetActiveAsync(
        Guid productId,
        bool isActive,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        var product = await dbContext.Products.SingleOrDefaultAsync(item => item.Id == productId, cancellationToken);
        if (product is null) return new(false, ["El producto ya no existe."]);
        if (product.IsActive == isActive) return new(true, []);

        var now = timeProvider.GetUtcNow();
        if (isActive) product.Activate(now); else product.Deactivate(now);
        dbContext.AuditEntries.Add(new AuditEntry
        {
            Id = Guid.NewGuid(), UserId = userId, Action = isActive ? "ProductActivated" : "ProductDeactivated",
            EntityType = nameof(Product), EntityId = productId.ToString(),
            PreviousValuesJson = JsonSerializer.Serialize(new { IsActive = !isActive }),
            NewValuesJson = JsonSerializer.Serialize(new { IsActive = isActive }), OccurredAtUtc = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(true, []);
    }

    public async Task<ProductStateResult> SetPublishedAsync(
        Guid productId,
        bool isPublished,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        var publication = await dbContext.ProductPublications.SingleOrDefaultAsync(item => item.ProductId == productId, cancellationToken);
        if (publication is null) return new(false, ["La publicación del producto ya no existe."]);
        if (publication.IsPublished == isPublished) return new(true, []);

        var now = timeProvider.GetUtcNow();
        if (isPublished) publication.Publish(now); else publication.Hide();
        dbContext.AuditEntries.Add(new AuditEntry
        {
            Id = Guid.NewGuid(), UserId = userId, Action = isPublished ? "ProductPublished" : "ProductHidden",
            EntityType = nameof(ProductPublication), EntityId = productId.ToString(),
            PreviousValuesJson = JsonSerializer.Serialize(new { IsPublished = !isPublished }),
            NewValuesJson = JsonSerializer.Serialize(new { IsPublished = isPublished }), OccurredAtUtc = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(true, []);
    }

    public async Task<CreateProductResult> CreateProductAsync(
        CreateProductRequest request,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return new CreateProductResult(false, null, errors);
        }

        var slug = request.Slug.Trim().ToLowerInvariant();
        if (await dbContext.Products.AnyAsync(product => product.Slug == slug, cancellationToken))
        {
            return new CreateProductResult(false, null, ["Ya existe un producto con esa URL."]);
        }

        var now = timeProvider.GetUtcNow();
        var product = Product.Create(request.Name, slug, request.Presentation, now);
        var price = new ProductPrice(Guid.NewGuid(), product.Id, request.StandardPrice, now);
        var publication = new ProductPublication(product.Id);
        publication.Configure(
            request.PublicDescription,
            request.ApprovedBenefits,
            request.IsFeatured,
            request.IsLandingFeatured,
            request.ShowPublicPrice,
            displayOrder: 0,
            request.LandingOrder,
            request.LandingTitle,
            request.LandingSubtitle,
            request.LandingBadgeText);
        if (request.Publish)
        {
            publication.Publish(now);
        }

        dbContext.Products.Add(product);
        dbContext.ProductPrices.Add(price);
        dbContext.ProductPublications.Add(publication);
        dbContext.AuditEntries.Add(new AuditEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = "ProductCreated",
            EntityType = nameof(Product),
            EntityId = product.Id.ToString(),
            NewValuesJson = JsonSerializer.Serialize(new { product.Name, product.Slug, request.StandardPrice }),
            OccurredAtUtc = now
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return new CreateProductResult(true, product.Id, []);
    }

    public async Task<CreateProductResult> UpdateProductAsync(
        Guid productId,
        CreateProductRequest request,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(request);
        if (errors.Count > 0) return new(false, null, errors);

        var product = await dbContext.Products.SingleOrDefaultAsync(item => item.Id == productId, cancellationToken);
        var publication = await dbContext.ProductPublications.SingleOrDefaultAsync(item => item.ProductId == productId, cancellationToken);
        if (product is null || publication is null) return new(false, null, ["El producto ya no existe."]);

        var slug = request.Slug.Trim().ToLowerInvariant();
        if (await dbContext.Products.AnyAsync(item => item.Id != productId && item.Slug == slug, cancellationToken))
            return new(false, null, ["Ya existe un producto con esa URL."]);

        var now = timeProvider.GetUtcNow();
        var previous = new { product.Name, product.Slug, product.Presentation, publication.IsPublished };
        product.Update(request.Name, slug, request.Presentation, product.CategoryId, now);
        publication.Configure(
            request.PublicDescription,
            request.ApprovedBenefits,
            request.IsFeatured,
            request.IsLandingFeatured,
            request.ShowPublicPrice,
            publication.DisplayOrder,
            request.LandingOrder,
            request.LandingTitle,
            request.LandingSubtitle,
            request.LandingBadgeText);
        if (request.Publish) publication.Publish(now); else publication.Hide();

        var currentPrice = await dbContext.ProductPrices
            .Where(price => price.ProductId == productId && price.EffectiveToUtc == null)
            .OrderByDescending(price => price.EffectiveFromUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (currentPrice is null || currentPrice.Amount != request.StandardPrice)
        {
            if (currentPrice is not null) currentPrice.Close(now);
            dbContext.ProductPrices.Add(new ProductPrice(Guid.NewGuid(), productId, request.StandardPrice, now));
        }

        dbContext.AuditEntries.Add(new AuditEntry
        {
            Id = Guid.NewGuid(), UserId = userId, Action = "ProductUpdated", EntityType = nameof(Product),
            EntityId = productId.ToString(), PreviousValuesJson = JsonSerializer.Serialize(previous),
            NewValuesJson = JsonSerializer.Serialize(new { product.Name, product.Slug, product.Presentation, request.StandardPrice, request.Publish }),
            OccurredAtUtc = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(true, productId, []);
    }

    private static List<string> Validate(CreateProductRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.Name)) errors.Add("El nombre es obligatorio.");
        if (string.IsNullOrWhiteSpace(request.Slug)) errors.Add("La URL es obligatoria.");
        if (string.IsNullOrWhiteSpace(request.Presentation)) errors.Add("La presentación es obligatoria.");
        if (string.IsNullOrWhiteSpace(request.PublicDescription)) errors.Add("La descripción pública es obligatoria.");
        if (request.StandardPrice < 0) errors.Add("El precio no puede ser negativo.");
        if (request.LandingOrder < 0) errors.Add("El orden de landing no puede ser negativo.");
        if (request.LandingTitle?.Length > 160) errors.Add("El título de landing admite hasta 160 caracteres.");
        if (request.LandingSubtitle?.Length > 300) errors.Add("El subtítulo de landing admite hasta 300 caracteres.");
        if (request.LandingBadgeText?.Length > 120) errors.Add("La insignia de landing admite hasta 120 caracteres.");
        return errors;
    }
}
