namespace TentacionSana.Application.Receivables;

public static class PaymentDistribution
{
    public static IReadOnlyList<ManualPaymentAllocation> Build(
        IReadOnlyList<ReceivableOrderItem> orders, IReadOnlyList<Guid> selectedOrderIds,
        decimal amount, bool automatic, IReadOnlyList<ManualPaymentAllocation> manual)
    {
        if (amount <= 0 || decimal.Round(amount, 2) != amount)
            throw new ArgumentException("El monto debe ser positivo y tener como máximo dos decimales.");
        var ids = selectedOrderIds.ToHashSet();
        var selected = orders.Where(x => ids.Contains(x.OrderId) && x.Balance > 0)
            .OrderBy(x => x.DeliveredAtUtc ?? DateTimeOffset.MaxValue).ThenBy(x => x.OrderNumber).ToList();
        if (ids.Count == 0 || selected.Count != ids.Count)
            throw new ArgumentException("Selecciona pedidos pendientes de este responsable. Si sus saldos cambiaron, actualiza la cuenta.");
        if (amount > selected.Sum(x => x.Balance))
            throw new ArgumentException("El pago supera el saldo de los pedidos seleccionados.");
        if (automatic)
        {
            var result = new List<ManualPaymentAllocation>();
            var remaining = amount;
            foreach (var order in selected)
            {
                var applied = Math.Min(remaining, order.Balance);
                if (applied > 0) result.Add(new(order.OrderId, applied));
                remaining -= applied;
            }
            return result;
        }
        if (manual.Any(x => x.Amount < 0 || decimal.Round(x.Amount, 2) != x.Amount || !ids.Contains(x.OrderId)) ||
            manual.Select(x => x.OrderId).Distinct().Count() != manual.Count)
            throw new ArgumentException("Las aplicaciones deben ser únicas, válidas y pertenecer a los pedidos seleccionados.");
        var requested = manual.Where(x => x.Amount > 0).ToList();
        if (requested.Sum(x => x.Amount) != amount)
            throw new ArgumentException("La suma aplicada a pedidos debe coincidir con el monto recibido.");
        if (requested.Any(x => x.Amount > selected.Single(o => o.OrderId == x.OrderId).Balance))
            throw new ArgumentException("Una aplicación supera el saldo disponible del pedido.");
        return requested;
    }
}
