using CloudinaryDotNet;
using Microsoft.Extensions.Logging;

namespace TentacionSana.Infrastructure.Media;

internal static class CloudinaryPrivateMedia
{
    private static readonly Action<ILogger, string, int, string?, Exception?> DownloadRejected =
        LoggerMessage.Define<string, int, string?>(LogLevel.Warning,
            new EventId(1, nameof(DownloadRejected)),
            "Cloudinary {DownloadMethod} returned HTTP {StatusCode} for {FileName}.");
    private static readonly Action<ILogger, string, string?, Exception?> DownloadFailed =
        LoggerMessage.Define<string, string?>(LogLevel.Warning,
            new EventId(2, nameof(DownloadFailed)),
            "Cloudinary {DownloadMethod} failed for {FileName}.");

    public static async Task<(byte[] Content, string ContentType)?> DownloadAsync(
        CloudinaryOptions settings,
        IHttpClientFactory httpClientFactory,
        string publicId,
        string format,
        string? fileName,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken,
        ILogger? logger = null)
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
        var client = httpClientFactory.CreateClient();

        // Render only needs the image bytes; a signed authenticated CDN URL avoids the
        // time-sensitive download API while keeping the URL on the server.
        var source = string.IsNullOrWhiteSpace(format)
            || publicId.EndsWith($".{format}", StringComparison.OrdinalIgnoreCase)
                ? publicId
                : $"{publicId}.{format}";
        var signedCdnUrl = cloudinary.Api.UrlImgUp.Clone()
            .Type("authenticated")
            .Signed(true)
            .Secure(true)
            .BuildUrl(source);
        var downloaded = await TryDownloadAsync(client, signedCdnUrl, format, fileName,
            "signed CDN download", logger, cancellationToken);
        if (downloaded is not null) return downloaded;

        // Keep the time-limited API URL as a fallback for Cloudinary environments that
        // don't accept signed delivery URLs for the original resource.
        var privateDownloadUrl = cloudinary.DownloadPrivate(publicId, false, format, "authenticated",
            expiresAt.ToUnixTimeSeconds(), "image", null, fileName);
        return await TryDownloadAsync(client, privateDownloadUrl, format, fileName,
            "private download", logger, cancellationToken);
    }

    private static async Task<(byte[] Content, string ContentType)?> TryDownloadAsync(
        HttpClient client,
        string url,
        string format,
        string? fileName,
        string method,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                if (logger is not null) DownloadRejected(logger, method, (int)response.StatusCode, fileName, null);
                return null;
            }

            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (content.Length == 0)
            {
                if (logger is not null) DownloadRejected(logger, method, (int)response.StatusCode, fileName, null);
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? ContentTypeFromFormat(format);
            return (content, contentType);
        }
        catch (HttpRequestException exception)
        {
            if (logger is not null) DownloadFailed(logger, method, fileName, exception);
            return null;
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            if (logger is not null) DownloadFailed(logger, method, fileName, exception);
            return null;
        }
    }

    private static string ContentTypeFromFormat(string format) => format.ToLowerInvariant() switch
    {
        "jpg" or "jpeg" => "image/jpeg",
        "webp" => "image/webp",
        _ => "image/png"
    };
}
