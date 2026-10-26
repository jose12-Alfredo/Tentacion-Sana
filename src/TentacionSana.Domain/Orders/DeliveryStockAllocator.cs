using TentacionSana.Domain.Inventory;

namespace TentacionSana.Domain.Orders;

public static class DeliveryStockAllocator
{
    public static void Consume(
        ProductStockBalance balance,
        StockReservation target,
        IEnumerable<StockReservation> otherReservations,
        int quantity)
    {
        if (quantity < 1 || quantity > target.Quantity || !target.IsActive)
            throw new InvalidOperationException("La cantidad entregada supera lo pendiente del pedido.");
        if (quantity > balance.PhysicalQuantity)
            throw new InvalidOperationException($"Faltan {quantity - balance.PhysicalQuantity} unidades de producto para confirmar la entrega: se intentan entregar {quantity} y hay {balance.PhysicalQuantity} en inventario.");

        var candidates = otherReservations
            .Where(x => x.Id != target.Id && x.IsActive && x.ProductId == target.ProductId && x.ReservedQuantity > 0)
            .ToList();
        var neededFromOthers = Math.Max(0, quantity - target.ReservedQuantity - balance.AvailableQuantity);
        if (candidates.Sum(x => x.ReservedQuantity) < neededFromOthers)
            throw new InvalidOperationException("Las reservas de inventario no coinciden con el stock físico. Revisa el inventario.");

        foreach (var candidate in candidates)
        {
            if (neededFromOthers == 0) break;
            var released = Math.Min(neededFromOthers, candidate.ReservedQuantity);
            candidate.Allocate(candidate.ReservedQuantity - released);
            balance.Release(released);
            neededFromOthers -= released;
        }

        var neededForTarget = quantity - target.ReservedQuantity;
        if (neededForTarget > 0)
        {
            var allocated = balance.Reserve(neededForTarget);
            if (allocated != neededForTarget)
                throw new InvalidOperationException("No hay existencias físicas suficientes para confirmar la entrega.");
            target.Allocate(target.ReservedQuantity + allocated);
        }

        balance.DeliverReserved(quantity);
        target.Consume(quantity);
    }
}
