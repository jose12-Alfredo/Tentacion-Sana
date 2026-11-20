using TentacionSana.Application.Receivables;

namespace TentacionSana.UnitTests.Receivables;

public sealed class PaymentDistributionTests
{
    private static ReceivableOrderItem Order(string branch, long number, decimal balance, int days) =>
        new(Guid.NewGuid(), number, "Nutriarte", branch, DateTimeOffset.UtcNow.AddDays(-days), balance, 0, balance, 1, days, "Unpaid", 0, 0);

    [Fact]
    public void AutomaticDistributionExcludesOlderDebtOfUnselectedBranch()
    {
        var excluded = Order("Equipetrol", 1001, 500, 30);
        var makro = Order("Makro", 1101, 180, 10);
        var other = Order("Centro", 1102, 150, 5);
        var result = PaymentDistribution.Build([excluded, other, makro], [makro.OrderId, other.OrderId], 200, true, []);
        Assert.Equal(2, result.Count);
        Assert.Equal(new ManualPaymentAllocation(makro.OrderId, 180), result[0]);
        Assert.Equal(new ManualPaymentAllocation(other.OrderId, 20), result[1]);
        Assert.DoesNotContain(result, x => x.OrderId == excluded.OrderId);
    }

    [Fact]
    public void ManualPaymentCanPaySpecificOrdersAcrossSelectedBranches()
    {
        var first = Order("Makro", 1101, 180, 10);
        var second = Order("Equipetrol", 1102, 150, 5);
        var result = PaymentDistribution.Build([first, second], [first.OrderId, second.OrderId], 100, false,
            [new(first.OrderId, 60), new(second.OrderId, 40)]);
        Assert.Equal(100, result.Sum(x => x.Amount));
    }

    [Fact]
    public void RejectsEmptySelectionUnknownOrderAndAmountAboveSelectedBalance()
    {
        var order = Order("Makro", 1101, 180, 10);
        Assert.Throws<ArgumentException>(() => PaymentDistribution.Build([order], [], 10, true, []));
        Assert.Throws<ArgumentException>(() => PaymentDistribution.Build([order], [Guid.NewGuid()], 10, true, []));
        Assert.Throws<ArgumentException>(() => PaymentDistribution.Build([order], [order.OrderId], 181, true, []));
    }

    [Fact]
    public void RejectsManualApplicationsOutsideSelectionAndInvalidAmounts()
    {
        var order = Order("Makro", 1101, 180, 10);
        Assert.Throws<ArgumentException>(() => PaymentDistribution.Build([order], [order.OrderId], 10, false, [new(Guid.NewGuid(), 10)]));
        Assert.Throws<ArgumentException>(() => PaymentDistribution.Build([order], [order.OrderId], 10, false, [new(order.OrderId, 9)]));
        Assert.Throws<ArgumentException>(() => PaymentDistribution.Build([order], [order.OrderId], 10, false, [new(order.OrderId, 5), new(order.OrderId, 5)]));
        Assert.Throws<ArgumentException>(() => PaymentDistribution.Build([order], [order.OrderId], 10.001m, true, []));
        Assert.Throws<ArgumentException>(() => PaymentDistribution.Build([order], [order.OrderId], 0, true, []));
    }
}
