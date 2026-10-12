using TentacionSana.Domain.Inventory;
using TentacionSana.Domain.Orders;

namespace TentacionSana.UnitTests.Orders;

public sealed class DeliveryStockAllocatorTests
{
    [Fact]
    public void CanDeliverFourthOrderFirstWithoutChangingProductionShortage()
    {
        var productId = Guid.NewGuid();
        var balance = ProductStockBalance.Create(productId);
        balance.Produce(50);
        var reservations = Enumerable.Range(0, 4)
            .Select(_ => CreateReservation(productId, 30))
            .ToList();

        foreach (var reservation in reservations)
            reservation.Allocate(balance.Reserve(reservation.Quantity));

        Assert.Equal(70, reservations.Sum(x => x.ShortageQuantity));

        DeliveryStockAllocator.Consume(balance, reservations[3], reservations.Take(3), 30);

        Assert.Equal(20, balance.PhysicalQuantity);
        Assert.Equal(20, balance.ReservedQuantity);
        Assert.False(reservations[3].IsActive);
        Assert.Equal(70, reservations.Sum(x => x.ShortageQuantity));
    }

    [Fact]
    public void DeliveryWithInsufficientPhysicalStockDoesNotMoveReservations()
    {
        var productId = Guid.NewGuid();
        var balance = ProductStockBalance.Create(productId);
        balance.Produce(20);
        var first = CreateReservation(productId, 30);
        var second = CreateReservation(productId, 30);
        first.Allocate(balance.Reserve(first.Quantity));

        Assert.Throws<InvalidOperationException>(() =>
            DeliveryStockAllocator.Consume(balance, second, [first], 30));

        Assert.Equal(20, balance.PhysicalQuantity);
        Assert.Equal(20, balance.ReservedQuantity);
        Assert.Equal(20, first.ReservedQuantity);
        Assert.Equal(0, second.ReservedQuantity);
    }

    private static StockReservation CreateReservation(Guid productId, int quantity)
    {
        var userId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero);
        var order = Order.CreateDraft(Guid.NewGuid(), null, userId, now);
        order.Configure(order.CustomerId, null, null, null, now.AddDays(1), null, userId, now);
        order.AddLine(productId, "Producto", quantity, 10m, 10m, null, userId, now);
        order.Confirm(OrderSnapshot.Create(order.Id, "Cliente", null, null, null, null, null, now), userId, now);
        return Assert.Single(order.Reservations);
    }
}
