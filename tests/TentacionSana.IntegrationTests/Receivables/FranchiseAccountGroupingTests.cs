using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TentacionSana.Domain.Customers;
using TentacionSana.Domain.Deliveries;
using TentacionSana.Infrastructure.Customers;
using TentacionSana.Infrastructure.Media;
using TentacionSana.Infrastructure.Receivables;
using TentacionSana.Infrastructure.Orders;
using TentacionSana.IntegrationTests.Deliveries;

namespace TentacionSana.IntegrationTests.Receivables;

public sealed class FranchiseAccountGroupingTests
{
    [Fact]
    public async Task NewOrderUsesPointsAccountWithoutChangingExistingOrders()
    {
        await using var fixture = await DeliveryDateTests.Fixture.CreateAsync();
        var customer = await fixture.Db.Customers.SingleAsync();
        var points = new CustomerManagementService(fixture.Db, TimeProvider.System);
        var created = await points.AddDeliveryPointAsync(customer.Id, new("Franchise", "Nueva franquicia", "Dirección", null, null, null, null, null), fixture.UserId);
        Assert.True(created.Succeeded);
        var point = await fixture.Db.DeliveryPoints.SingleAsync(x => x.Id == created.Id);
        var orders = new OrderManagementService(fixture.Db, TimeProvider.System, null!, null!);
        var draft = await orders.CreateDraftAsync(customer.Id, fixture.UserId);
        Assert.True(draft.Succeeded);
        var configured = await orders.ConfigureDraftAsync(draft.Id!.Value, 1, new(customer.Id, point.Id, null, null, DateTimeOffset.UtcNow, null), fixture.UserId);
        Assert.True(configured.Succeeded, string.Join(" ", configured.Errors));
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(point.PaymentResponsiblePartyId, (await fixture.Db.Orders.SingleAsync(x => x.Id == draft.Id)).PaymentResponsiblePartyId);
        Assert.Null((await fixture.Db.Orders.SingleAsync(x => x.Id == fixture.OrderId)).PaymentResponsiblePartyId);
        var centralPoint = await points.AddDeliveryPointAsync(customer.Id, new("Branch", "Sucursal propia", "Dirección", null, null, null, null, null), fixture.UserId);
        Assert.True(centralPoint.Succeeded);
        var central = await fixture.Db.DeliveryPoints.SingleAsync(x => x.Id == centralPoint.Id);
        var current = await fixture.Db.Orders.AsNoTracking().SingleAsync(x => x.Id == draft.Id);
        var rejected = await orders.ConfigureDraftAsync(current.Id, current.Version,
            new(customer.Id, point.Id, null, central.PaymentResponsiblePartyId, DateTimeOffset.UtcNow, null), fixture.UserId);
        Assert.False(rejected.Succeeded);
        Assert.Equal(point.PaymentResponsiblePartyId, (await fixture.Db.Orders.AsNoTracking().SingleAsync(x => x.Id == draft.Id)).PaymentResponsiblePartyId);
    }

    [Fact]
    public async Task NewPointsCreatePersistedAccountsAndOverviewSeparatesBalances()
    {
        await using var fixture = await DeliveryDateTests.Fixture.CreateAsync();
        var customer = await fixture.Db.Customers.SingleAsync();
        var service = new CustomerManagementService(fixture.Db, TimeProvider.System);
        var first = await service.AddDeliveryPointAsync(customer.Id, new("Franchise", "Palacio", "Dirección", null, null, null, null, null), fixture.UserId);
        Assert.True(first.Succeeded, string.Join(" ", first.Errors));
        var second = await service.AddDeliveryPointAsync(customer.Id, new("Franchise", "Otra franquicia", "Dirección", null, null, null, null, null), fixture.UserId);
        Assert.True(second.Succeeded, string.Join(" ", second.Errors));
        var branch = await service.AddDeliveryPointAsync(customer.Id, new("Branch", "Makro", "Dirección", null, null, null, null, null), fixture.UserId);
        Assert.True(branch.Succeeded, string.Join(" ", branch.Errors));
        fixture.Db.ChangeTracker.Clear();
        var points = await fixture.Db.DeliveryPoints.ToListAsync();
        Assert.Equal(3, points.Select(x => x.PaymentResponsiblePartyId).Distinct().Count());
        foreach (var point in points)
        {
            var order = TentacionSana.Domain.Orders.Order.CreateDraft(customer.Id, null, fixture.UserId, DateTimeOffset.UtcNow);
            fixture.Db.Orders.Add(order);
            var account = Receivable.Create(order.Id, customer.Id, point.PaymentResponsiblePartyId);
            account.AddSale(point.Kind == DeliveryPointKind.Franchise ? 100 : 300);
            fixture.Db.Receivables.Add(account);
        }
        await fixture.Db.SaveChangesAsync();
        var accounts = new ReceivablesService(fixture.Db, TimeProvider.System, Options.Create(new CloudinaryOptions()), null!, null!, null!);
        var overview = await accounts.GetOverviewAsync();
        Assert.Equal(500m, overview.TotalPending);
        Assert.Equal(3, overview.ResponsibleParties.Count);
        Assert.Equal(2, overview.ResponsibleParties.Count(x => x.AccountKind == "Franchise" && x.Balance == 100));
        Assert.Equal(300m, Assert.Single(overview.ResponsibleParties, x => x.AccountKind == "Central").Balance);
    }
}
