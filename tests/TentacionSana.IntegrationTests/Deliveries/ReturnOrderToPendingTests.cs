using Microsoft.EntityFrameworkCore;
using TentacionSana.Domain.Deliveries;
using TentacionSana.Domain.Orders;

namespace TentacionSana.IntegrationTests.Deliveries;

public sealed class ReturnOrderToPendingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 16, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(1, 0)]
    [InlineData(3, 0)]
    [InlineData(3, 1)]
    [InlineData(3, 2)]
    public async Task ReturnsOnlyRequestedOrderAndCompactsRoutePositions(int routeSize, int targetPosition)
    {
        await using var fixture = await DeliveryDateTests.Fixture.CreateAsync();
        Assert.True((await fixture.Service.StartAsync(fixture.DeliveryId, 1, fixture.UserId)).Succeeded);
        var deliveryIds = new List<Guid>();
        var product = await fixture.Db.Products.SingleAsync();
        var customerId = (await fixture.Db.Orders.SingleAsync()).CustomerId;
        for (var index = 0; index < routeSize; index++)
        {
            if (index == targetPosition) { deliveryIds.Add(fixture.DeliveryId); continue; }
            var siblingOrder = Order.CreateDraft(customerId, null, fixture.UserId, Now);
            siblingOrder.Configure(customerId, null, null, null, Now, null, fixture.UserId, Now);
            var line = siblingOrder.AddLine(product.Id, product.Name, 1, 15m, 15m, null, fixture.UserId, Now);
            siblingOrder.Confirm(OrderSnapshot.Create(siblingOrder.Id, "Cliente", null, null, null, null, null, Now), fixture.UserId, Now);
            siblingOrder.AdvanceTo(OrderStatus.InPreparation, "Preparado", fixture.UserId, Now);
            siblingOrder.AdvanceTo(OrderStatus.Ready, "Listo", fixture.UserId, Now);
            var siblingDelivery = Delivery.Create(siblingOrder.Id, fixture.UserId, Now, [(line.Id, 1)], fixture.UserId, Now);
            fixture.Db.Orders.Add(siblingOrder);
            fixture.Db.Deliveries.Add(siblingDelivery);
            deliveryIds.Add(siblingDelivery.Id);
        }
        var route = DeliveryRoute.Create(fixture.UserId, Now, deliveryIds, fixture.UserId, Now);
        fixture.Db.DeliveryRoutes.Add(route);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();

        var result = await fixture.Service.ReturnOrderToPendingAsync(fixture.OrderId, fixture.UserId);

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Equal(OrderStatus.Ready, (await fixture.Db.Orders.AsNoTracking().SingleAsync(x => x.Id == fixture.OrderId)).Status);
        var returnedDelivery = await fixture.Db.Deliveries.AsNoTracking().Include(x => x.Lines).SingleAsync(x => x.Id == fixture.DeliveryId);
        Assert.Equal(DeliveryStatus.Assigned, returnedDelivery.Status);
        Assert.Equal(10, Assert.Single(returnedDelivery.Lines).AssignedQuantity);
        Assert.Equal(0, returnedDelivery.Lines[0].DeliveredQuantity);
        Assert.Equal(20, (await fixture.Db.ProductStockBalances.AsNoTracking().SingleAsync()).PhysicalQuantity);
        Assert.Empty(await fixture.Db.Sales.ToListAsync());
        Assert.Empty(await fixture.Db.Payments.ToListAsync());
        var remaining = await fixture.Db.DeliveryRouteStops.AsNoTracking().Where(x => x.DeliveryRouteId == route.Id).OrderBy(x => x.Position).ToListAsync();
        Assert.Equal(deliveryIds.Where(x => x != fixture.DeliveryId), remaining.Select(x => x.DeliveryId));
        Assert.Equal(Enumerable.Range(1, routeSize - 1), remaining.Select(x => x.Position));
        Assert.Equal(routeSize == 1 ? 0 : 1, await fixture.Db.DeliveryRoutes.CountAsync());
        Assert.All(await fixture.Db.Deliveries.AsNoTracking().Where(x => x.Id != fixture.DeliveryId).ToListAsync(), x => Assert.Equal(DeliveryStatus.Assigned, x.Status));

        Assert.True((await fixture.Service.ReturnOrderToPendingAsync(fixture.OrderId, fixture.UserId)).Succeeded);
        Assert.Single(await fixture.Db.AuditEntries.Where(x => x.Action == "DeliveryReturnedToPending").ToListAsync());
    }

    [Fact]
    public async Task RejectsReturnWhenDeliveryHasConfirmedUnits()
    {
        await using var fixture = await DeliveryDateTests.Fixture.CreateAsync();
        Assert.True((await fixture.CompleteAsync(Now)).Succeeded);
        var result = await fixture.Service.ReturnOrderToPendingAsync(fixture.OrderId, fixture.UserId);
        Assert.False(result.Succeeded);
        Assert.Equal(DeliveryStatus.Delivered, (await fixture.Db.Deliveries.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(150m, (await fixture.Db.Sales.AsNoTracking().SingleAsync()).Amount);
        Assert.Empty(await fixture.Db.AuditEntries.Where(x => x.Action == "DeliveryReturnedToPending").ToListAsync());
    }
}
