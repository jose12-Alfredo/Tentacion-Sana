using CloudinaryDotNet;

namespace TentacionSana.Infrastructure.Media;

internal static class CloudinaryPrivateMedia
{
    public static async Task<(byte[] Content, string ContentType)?> DownloadAsync(
        CloudinaryOptions settings,
        IHttpClientFactory httpClientFactory,
        string publicId,
        string format,
        string? fileName,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.CloudName)
            || string.IsNullOrWhiteSpace(settings.ApiKey)
            || string.IsNullOrWhiteSpace(settings.ApiSecret)
            || string.IsNullOrWhiteSpace(publicId))
        {
            return null;
        }

        var cloudinary = new Cloudinary(new Account(settings.CloudName, settings.ApiKey, settings.ApiSecret))
        {
            Api = { Secure = true }
        };
        var url = cloudinary.DownloadPrivate(publicId, false, format, "authenticated",
            expiresAt.ToUnixTimeSeconds(), "image", null, fileName);

        using var response = await httpClientFactory.CreateClient().GetAsync(
            url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? ContentTypeFromFormat(format);
        return (content, contentType);
    }

    private static string ContentTypeFromFormat(string format) => format.ToLowerInvariant() switch
    {
        "jpg" or "jpeg" => "image/jpeg",
        "webp" => "image/webp",
        _ => "image/png"
    };
}
