using TentacionSana.Application.Contact;

namespace TentacionSana.UnitTests.Contact;

public sealed class WhatsAppLinkBuilderTests
{
    [Fact]
    public void BuildRemovesFormattingAndIncludesProductContext()
    {
        var link = WhatsAppLinkBuilder.Build("+591 700-00-000", "Granola cacao", "Bolsa 250 g");

        Assert.NotNull(link);
        Assert.StartsWith("https://wa.me/59170000000?text=", link, StringComparison.Ordinal);
        Assert.Contains("Granola%20cacao", link, StringComparison.Ordinal);
        Assert.Contains("Cantidad%3A%20", link, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123")]
    public void BuildWithoutValidNumberReturnsNull(string? number)
    {
        Assert.Null(WhatsAppLinkBuilder.Build(number));
    }
}
