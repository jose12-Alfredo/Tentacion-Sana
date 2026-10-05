using CloudinaryDotNet;
using Microsoft.Extensions.Logging;

namespace TentacionSana.Infrastructure.Media;

internal static class CloudinaryPrivateMedia
{
    private static readonly Action<ILogger, int, string?, Exception?> PrivateDownloadRejected =
        LoggerMessage.Define<int, string?>(LogLevel.Warning, new EventId(1, nameof(PrivateDownloadRejected)),
            "Cloudinary private download returned HTTP {StatusCode} for {FileName}; trying the signed CDN URL.");
    private static readonly Action<ILogger, string?, Exception?> PrivateDownloadFailed =
        LoggerMessage.Define<string?>(LogLevel.Warning, new EventId(2, nameof(PrivateDownloadFailed)),
            "Cloudinary private download failed for {FileName}; trying the signed CDN URL.");
    private static readonly Action<ILogger, int, string?, Exception?> SignedCdnDownloadRejected =
        LoggerMessage.Define<int, string?>(LogLevel.Error, new EventId(3, nameof(SignedCdnDownloadRejected)),
            "Cloudinary signed CDN download returned HTTP {StatusCode} for {FileName}.");
    private static readonly Action<ILogger, string?, Exception?> SignedCdnDownloadFailed =
        LoggerMessage.Define<string?>(LogLevel.Error, new EventId(4, nameof(SignedCdnDownloadFailed)),
            "Cloudinary signed CDN download failed for {FileName}.");

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
        var privateDownloadUrl = cloudinary.DownloadPrivate(publicId, false, format, "authenticated",
            expiresAt.ToUnixTimeSeconds(), "image", null, fileName);
        var client = httpClientFactory.CreateClient();

        try
        {
            using var response = await client.GetAsync(
                privateDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await ReadAsync(response, format, cancellationToken);
            }

            if (logger is not null) PrivateDownloadRejected(logger, (int)response.StatusCode, fileName, null);
        }
        catch (HttpRequestException exception)
        {
            if (logger is not null) PrivateDownloadFailed(logger, fileName, exception);
        }

        var source = string.IsNullOrWhiteSpace(format)
            || publicId.EndsWith($".{format}", StringComparison.OrdinalIgnoreCase)
                ? publicId
                : $"{publicId}.{format}";
        var signedCdnUrl = cloudinary.Api.UrlImgUp.Clone()
            .Type("authenticated")
            .Signed(true)
            .Secure(true)
            .BuildUrl(source);

        try
        {
            using var response = await client.GetAsync(
                signedCdnUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await ReadAsync(response, format, cancellationToken);
            }

            if (logger is not null) SignedCdnDownloadRejected(logger, (int)response.StatusCode, fileName, null);
        }
        catch (HttpRequestException exception)
        {
            if (logger is not null) SignedCdnDownloadFailed(logger, fileName, exception);
        }

        return null;
    }

    private static async Task<(byte[] Content, string ContentType)> ReadAsync(
        HttpResponseMessage response,
        string format,
        CancellationToken cancellationToken)
    {
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
