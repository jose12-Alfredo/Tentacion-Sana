using TentacionSana.Application.Requests;

namespace TentacionSana.UnitTests.Requests;

public sealed class LocationLinkBuilderTests
{
    [Theory]
    [InlineData("https://maps.google.com/?q=-17.39,-66.15")]
    [InlineData("http://www.openstreetmap.org/?mlat=-17.39&mlon=-66.15")]
    public void BuildAcceptsAbsoluteHttpLinks(string location)
    {
        Assert.Equal(location, LocationLinkBuilder.Build(location));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Av. Siempre Viva 123")]
    [InlineData("javascript:alert(1)")]
    [InlineData("//maps.google.com/?q=-17.39,-66.15")]
    public void BuildRejectsTextAndUnsafeLinks(string? location)
    {
        Assert.Null(LocationLinkBuilder.Build(location));
    }
}
