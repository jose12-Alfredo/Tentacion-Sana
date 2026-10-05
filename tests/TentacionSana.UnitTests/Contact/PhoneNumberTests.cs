using TentacionSana.Domain.Contact;

namespace TentacionSana.UnitTests.Contact;

public sealed class PhoneNumberTests
{
    [Theory]
    [InlineData("700-00-000", "+59170000000")]
    [InlineData("+591 700 00 000", "+59170000000")]
    [InlineData("5491112345678", "+5491112345678")]
    public void NormalizeReturnsCanonicalInternationalNumber(string input, string expected)
    {
        Assert.Equal(expected, PhoneNumber.Normalize(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("1234567890123456")]
    public void NormalizeRejectsInvalidLength(string input)
    {
        Assert.Throws<ArgumentException>(() => PhoneNumber.Normalize(input));
    }
}
