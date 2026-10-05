using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;

namespace TentacionSana.Infrastructure.Deliveries;

public sealed record MapCoordinate(double Latitude, double Longitude);

public sealed partial class LocationCoordinateResolver(HttpClient http, IMemoryCache cache)
{
    private static readonly HashSet<string> GoogleHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "maps.app.goo.gl", "goo.gl", "google.com", "www.google.com", "maps.google.com"
    };

    public async Task<MapCoordinate?> ResolveAsync(string? location, string? address, string? reference, CancellationToken cancellationToken)
    {
        var key = $"map-coordinate-exact-v3:{location}";
        if (cache.TryGetValue<MapCoordinate>(key, out var cached)) return cached;

        var coordinate = TryExtract(location);
        if (coordinate is null && Uri.TryCreate(location, UriKind.Absolute, out var uri) && GoogleHosts.Contains(uri.Host))
            coordinate = await ResolveGoogleLinkAsync(uri, cancellationToken);
        if (coordinate is not null) cache.Set(key, coordinate, TimeSpan.FromDays(30));
        return coordinate;
    }

    private async Task<MapCoordinate?> ResolveGoogleLinkAsync(Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            for (var redirect = 0; redirect < 6; redirect++)
            {
                if (!GoogleHosts.Contains(uri.Host)) return null;
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } next)
                {
                    uri = next.IsAbsoluteUri ? next : new Uri(uri, next);
                    var redirected = TryExtract(uri.ToString());
                    if (redirected is not null) return redirected;
                    continue;
                }

                return TryExtract(response.RequestMessage?.RequestUri?.ToString());
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException) { }
        return null;
    }

    private static MapCoordinate? TryExtract(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var decoded = Uri.UnescapeDataString(value.Replace("\\u003d", "=").Replace("\\u0026", "&"));
        // Google Maps usa @lat,lng para el centro/zoom del mapa y !3dlat!4dlng
        // para el lugar seleccionado. El pin real debe tener prioridad.
        foreach (var regex in new[] { GoogleDataCoordinate(), QueryCoordinate(), AtCoordinate() })
        {
            var match = regex.Match(decoded);
            if (match.Success && Create(match.Groups[1].Value, match.Groups[2].Value) is { } coordinate) return coordinate;
        }
        return null;
    }

    private static MapCoordinate? Create(string latitude, string longitude)
    {
        if (!double.TryParse(latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
            !double.TryParse(longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var lng) ||
            Math.Abs(lat) > 90 || Math.Abs(lng) > 180) return null;
        return new MapCoordinate(lat, lng);
    }

    [GeneratedRegex(@"@(-?\d{1,2}(?:\.\d+)?),(-?\d{1,3}(?:\.\d+)?)")]
    private static partial Regex AtCoordinate();
    [GeneratedRegex(@"(?:q|query|destination|center)=(-?\d{1,2}(?:\.\d+)?)(?:%2C|,)(-?\d{1,3}(?:\.\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex QueryCoordinate();
    [GeneratedRegex(@"!3d(-?\d{1,2}(?:\.\d+)?)!4d(-?\d{1,3}(?:\.\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex GoogleDataCoordinate();
}
