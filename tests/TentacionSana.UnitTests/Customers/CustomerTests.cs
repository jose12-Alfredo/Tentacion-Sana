using TentacionSana.Domain.Customers;

namespace TentacionSana.UnitTests.Customers;

public sealed class CustomerTests
{
    [Fact]
    public void CompanySeparatesContactPaymentResponsibleAndDeliveryPoint()
    {
        var now = DateTimeOffset.UtcNow;
        var customer = Customer.Create("Nutriarte", "70000000", "ventas@nutriarte.test", now, CustomerKind.Company);
        var contact = customer.AddContact("Ana", "71111111", null, "Recepción", now);
        var payer = customer.AddPaymentResponsible(PaymentResponsibleKind.CentralCompany, "Nutriarte central", "72222222", null, now);
        var point = customer.AddDeliveryPoint(DeliveryPointKind.Branch, "Sucursal norte", "Av. Norte 10", "Puerta verde", "https://maps.example/point", contact.Id, payer.Id, now);

        Assert.Equal(CustomerKind.Company, customer.Kind);
        Assert.Equal(contact.Id, point.ContactId);
        Assert.Equal(payer.Id, point.PaymentResponsiblePartyId);
        Assert.Equal("+59170000000", customer.Phone);
    }

    [Fact]
    public void DeliveryPointRejectsContactFromAnotherCustomer()
    {
        var now = DateTimeOffset.UtcNow;
        var customer = Customer.Create("Cliente", "70000000", null, now);

        Assert.Throws<ArgumentException>(() => customer.AddDeliveryPoint(DeliveryPointKind.Other, "Casa", "Calle 1", null, null, Guid.NewGuid(), null, now));
    }
}
