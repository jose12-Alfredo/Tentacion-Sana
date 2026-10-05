namespace TentacionSana.Application.Catalog;

public interface IProductImageManagementService
{
    Task<UploadProductImageResult> UploadAsync(
        UploadProductImageRequest request,
        Guid? userId,
        CancellationToken cancellationToken = default);
}

public sealed record UploadProductImageRequest(
    Guid ProductId,
    Stream Content,
    string FileName,
    string ContentType,
    long Length,
    string AltText);

public sealed record UploadProductImageResult(bool Succeeded, IReadOnlyList<string> Errors);
