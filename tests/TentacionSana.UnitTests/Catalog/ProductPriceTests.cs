using TentacionSana.Domain.Catalog;

namespace TentacionSana.UnitTests.Catalog;

public sealed class ProductPriceTests
{
    [Fact]
    public void PriceRoundsMonetaryAmountToTwoDecimals()
    {
        var price = new ProductPrice(Guid.NewGuid(), Guid.NewGuid(), 12.345m, DateTimeOffset.UtcNow);

        Assert.Equal(12.35m, price.Amount);
    }

    [Fact]
    public void PriceRejectsNegativeAmount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProductPrice(Guid.NewGuid(), Guid.NewGuid(), -0.01m, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ClosingPriceRequiresLaterDate()
    {
        var start = DateTimeOffset.UtcNow;
        var price = new ProductPrice(Guid.NewGuid(), Guid.NewGuid(), 20m, start);

        Assert.Throws<InvalidOperationException>(() => price.Close(start));
    }
}
