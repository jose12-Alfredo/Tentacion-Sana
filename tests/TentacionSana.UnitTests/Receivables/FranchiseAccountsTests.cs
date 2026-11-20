using TentacionSana.Domain.Customers;
using TentacionSana.Domain.Deliveries;

namespace TentacionSana.UnitTests.Receivables;

public sealed class FranchiseAccountsTests
{
    [Fact]
    public void NewFranchisesHaveIndependentAccountsAndBranchesShareCentral()
    {
        var customer = Customer.Create("Nutriarte", null, null, DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        var first = customer.ResolvePointPayer(DeliveryPointKind.Franchise, "Palacio", null, null, now);
        var second = customer.ResolvePointPayer(DeliveryPointKind.Franchise, "Nueva franquicia", null, null, now);
        var central = customer.ResolvePointPayer(DeliveryPointKind.Branch, "Makro", null, null, now);
        var other = customer.ResolvePointPayer(DeliveryPointKind.Branch, "Abasto", null, null, now);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(central.Id, other.Id);
        Assert.Equal(PaymentResponsibleKind.Franchisee, first.Kind);
        Assert.Equal("", first.Phone);
        Assert.Equal(first.Id, customer.ResolvePointPayer(DeliveryPointKind.Franchise, "Palacio", null, null, now).Id);
    }

    [Fact]
    public void FranchiseCanShareOwnersAccountButCannotUseCentral()
    {
        var customer = Customer.Create("Nutriarte", null, null, DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        var owner = customer.ResolvePointPayer(DeliveryPointKind.Franchise, "Primera", null, null, now);
        Assert.Equal(owner.Id, customer.ResolvePointPayer(DeliveryPointKind.Franchise, "Segunda", owner.Id, null, now).Id);
        var central = customer.ResolvePointPayer(DeliveryPointKind.Branch, "Makro", null, null, now);
        Assert.Throws<ArgumentException>(() => customer.ResolvePointPayer(DeliveryPointKind.Franchise, "Tercera", central.Id, null, now));
    }

    [Fact]
    public void ReassigningUnpaidDebtDoesNotChangeAmountsAndRejectsPaidHistory()
    {
        var account = Receivable.Create(Guid.NewGuid(), Guid.NewGuid(), null);
        account.AddSale(225);
        var payer = Guid.NewGuid();
        account.ReassignUnpaidAccount(payer);
        Assert.Equal(payer, account.PayerId);
        Assert.Equal(225, account.Balance);
        account.ApplyPayment(50);
        Assert.Throws<InvalidOperationException>(() => account.ReassignUnpaidAccount(Guid.NewGuid()));
        Assert.Equal(payer, account.PayerId);
        Assert.Equal(50, account.PaidAmount);
    }
}
