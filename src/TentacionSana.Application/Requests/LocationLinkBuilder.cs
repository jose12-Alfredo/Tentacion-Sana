namespace TentacionSana.Application.Requests;

public static class LocationLinkBuilder
{
    public static string? Build(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)
            || !Uri.TryCreate(location.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        return uri.AbsoluteUri;
    }
}
