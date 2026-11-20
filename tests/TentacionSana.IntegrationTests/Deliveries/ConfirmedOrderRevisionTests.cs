using Microsoft.EntityFrameworkCore;
using TentacionSana.Domain.Orders;
using TentacionSana.Infrastructure.Orders;

namespace TentacionSana.IntegrationTests.Deliveries;

public sealed class ConfirmedOrderRevisionTests
{
    [Theory]
    [InlineData(12, 1)]
    [InlineData(8, 0)]
    [InlineData(8, 1)]
    public async Task RevisingQuantitiesUpdatesDeliveryReservationAndHistory(int saleQuantity, int tastingQuantity)
    {
        await using var fixture = await DeliveryDateTests.Fixture.CreateAsync();
        var order = await fixture.Db.Orders.Include(x => x.Lines).SingleAsync();
        fixture.Db.Entry(order).Property(x => x.Status).CurrentValue = OrderStatus.Confirmed;
        await fixture.Db.SaveChangesAsync();
        var lineId = order.Lines[0].Id;
        var version = order.Version;
        var service = new OrderManagementService(fixture.Db, TimeProvider.System, null!, null!);

        var result = await service.ReviseLineAsync(order.Id, lineId, version,
            new(saleQuantity, 0, tastingQuantity, 15m, null, null, null,
                tastingQuantity > 0 ? "Degustación en punto de venta" : null, null,
                "Corrección de cantidades según boleta"), fixture.UserId);

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        fixture.Db.ChangeTracker.Clear();
        var updatedLine = await fixture.Db.OrderLines.SingleAsync(x => x.Id == lineId);
        Assert.Equal(saleQuantity, updatedLine.SaleQuantity);
        Assert.Equal(tastingQuantity, updatedLine.TastingQuantity);
        var deliveryLine = await fixture.Db.DeliveryLines.SingleAsync();
        Assert.Equal(saleQuantity, deliveryLine.AssignedSaleQuantity);
        Assert.Equal(tastingQuantity, deliveryLine.AssignedTastingQuantity);
        Assert.Equal(saleQuantity + tastingQuantity, (await fixture.Db.StockReservations.SingleAsync()).ReservedQuantity);
        Assert.Equal(saleQuantity + tastingQuantity, (await fixture.Db.ProductStockBalances.SingleAsync()).ReservedQuantity);
        Assert.Equal(20, (await fixture.Db.ProductStockBalances.SingleAsync()).PhysicalQuantity);
        Assert.NotEmpty(await fixture.Db.DeliveryStatusHistory.Where(x => x.Reason == "Cantidad ajustada por modificación del pedido").ToListAsync());
        Assert.Empty(await fixture.Db.Sales.ToListAsync());
    }

    [Fact]
    public async Task StaleVersionDoesNotOverwriteTheOtherSessionsRevision()
    {
        await using var fixture = await DeliveryDateTests.Fixture.CreateAsync();
        var order = await fixture.Db.Orders.Include(x => x.Lines).SingleAsync();
        fixture.Db.Entry(order).Property(x => x.Status).CurrentValue = OrderStatus.Confirmed;
        await fixture.Db.SaveChangesAsync();
        var version = order.Version;
        var lineId = order.Lines[0].Id;
        var service = new OrderManagementService(fixture.Db, TimeProvider.System, null!, null!);
        Assert.True((await service.ReviseLineAsync(order.Id, lineId, version,
            new(11, 0, 0, 15m, null, null, null, null, null, "Cambio desde otra sesión"), fixture.UserId)).Succeeded);

        var conflict = await service.ReviseLineAsync(order.Id, lineId, version,
            new(12, 0, 0, 15m, null, null, null, null, null, "Edición con versión anterior"), fixture.UserId);

        Assert.False(conflict.Succeeded);
        Assert.Equal("ConcurrencyConflict", conflict.ErrorCode);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(11, (await fixture.Db.OrderLines.SingleAsync()).SaleQuantity);
        Assert.Equal(11, (await fixture.Db.DeliveryLines.SingleAsync()).AssignedSaleQuantity);
        var refreshed = await fixture.Db.Orders.SingleAsync();
        Assert.True((await service.ReviseLineAsync(refreshed.Id, lineId, refreshed.Version,
            new(12, 0, 0, 15m, null, null, null, null, null, "Guardar tras consultar la versión actual"), fixture.UserId)).Succeeded);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(12, (await fixture.Db.OrderLines.SingleAsync()).SaleQuantity);
        Assert.Equal(12, (await fixture.Db.DeliveryLines.SingleAsync()).AssignedSaleQuantity);
    }
}
