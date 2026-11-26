namespace TentacionSana.Application.Orders;

public sealed record NoteOrderLineSource(Guid Id, string Name, int Sale, int Replacement, int Tasting, decimal Price,
    string? ReplacementReason = null, string? ReplacementNotes = null, string? TastingReason = null, string? TastingNotes = null);
public sealed record NoteDeliverySource(Guid Id, string Status, int Version, DateTimeOffset ScheduledAtUtc, DateTimeOffset? CompletedAtUtc,
    string? Driver, string? Receiver, IReadOnlyList<NoteDeliveryLineSource> Lines);
public sealed record NoteDeliveryLineSource(Guid OrderLineId, int AssignedSale, int AssignedReplacement, int AssignedTasting,
    int DeliveredSale, int DeliveredReplacement, int DeliveredTasting);

public static class DeliveryNoteProjection
{
    public static IReadOnlyList<ThermalNoteLine> SelectLines(IReadOnlyList<NoteOrderLineSource> orderLines,
        IReadOnlyList<NoteDeliverySource> deliveries, Guid? deliveryId, bool registeredSummary)
    {
        var selected = deliveryId is { } id ? deliveries.SingleOrDefault(x => x.Id == id)
            ?? throw new ArgumentException("La entrega seleccionada no pertenece al pedido.") : null;
        if (registeredSummary && (selected is null || selected.Status != "Delivered"))
            throw new ArgumentException("El resumen requiere una entrega completada verificable.");
        if (!registeredSummary && selected?.Status is "Delivered" or "Cancelled" or "Failed")
            throw new ArgumentException("Esta entrega no tiene una salida pendiente disponible.");
        if (selected is null && deliveries.Any(x => x.Status is "Scheduled" or "Assigned" or "InRoute" or "Partial"))
            throw new ArgumentException("Hay entregas asignadas. Selecciona la entrega que se va a documentar.");
        var result = new List<ThermalNoteLine>();
        foreach (var line in orderLines)
        {
            var all = deliveries.SelectMany(x => x.Lines).Where(x => x.OrderLineId == line.Id).ToList();
            if (all.Any(x => x.DeliveredSale < 0 || x.DeliveredReplacement < 0 || x.DeliveredTasting < 0) ||
                all.Sum(x => x.DeliveredSale) > line.Sale || all.Sum(x => x.DeliveredReplacement) > line.Replacement || all.Sum(x => x.DeliveredTasting) > line.Tasting)
                throw new ArgumentException("Las entregas acumuladas superan las cantidades verificables del pedido.");
            var matching = selected?.Lines.Where(x => x.OrderLineId == line.Id).ToList();
            var sale = selected is null ? line.Sale - all.Sum(x => x.DeliveredSale) :
                matching!.Sum(x => registeredSummary ? x.DeliveredSale : x.AssignedSale - x.DeliveredSale);
            var replacement = selected is null ? line.Replacement - all.Sum(x => x.DeliveredReplacement) :
                matching!.Sum(x => registeredSummary ? x.DeliveredReplacement : x.AssignedReplacement - x.DeliveredReplacement);
            var tasting = selected is null ? line.Tasting - all.Sum(x => x.DeliveredTasting) :
                matching!.Sum(x => registeredSummary ? x.DeliveredTasting : x.AssignedTasting - x.DeliveredTasting);
            if (sale < 0 || replacement < 0 || tasting < 0 || sale > line.Sale || replacement > line.Replacement || tasting > line.Tasting)
                throw new ArgumentException("Las cantidades del pedido y de sus entregas no coinciden. Revisa la entrega antes de emitir.");
            if (!registeredSummary && (sale > line.Sale - all.Sum(x => x.DeliveredSale) ||
                replacement > line.Replacement - all.Sum(x => x.DeliveredReplacement) || tasting > line.Tasting - all.Sum(x => x.DeliveredTasting)))
                throw new ArgumentException("Las cantidades asignadas superarían lo pendiente del pedido.");
            if (sale + replacement + tasting > 0)
                result.Add(new(line.Id, line.Name, sale, replacement, tasting, line.Price,
                    line.ReplacementReason, line.ReplacementNotes, line.TastingReason, line.TastingNotes));
        }
        if (selected?.Lines.Any(x => orderLines.All(l => l.Id != x.OrderLineId)) == true)
            throw new ArgumentException("La entrega contiene líneas retiradas o desconocidas.");
        if (result.Count == 0) throw new ArgumentException("No hay unidades pendientes para esta nota.");
        return result;
    }
    public static decimal DeliveryFee(decimal fee, IReadOnlyList<NoteDeliverySource> deliveries, Guid? selectedId, bool summary)
    {
        var completed = deliveries.Where(x => x.CompletedAtUtc is not null).OrderBy(x => x.CompletedAtUtc).ThenBy(x => x.Id).ToList();
        // El modelo histórico no guarda un cargo de envío por entrega. Con varias
        // entregas acumuladas no se atribuye retrospectivamente a una de ellas.
        if (summary) return completed.Count == 1 && completed[0].Id == selectedId ? fee : 0;
        if (completed.Count > 0) return 0;
        // Se asigna el envío a una sola salida planificada, sin reservar ni cobrar nada.
        var first = deliveries.Where(x => x.Status is "Scheduled" or "Assigned" or "InRoute" or "Partial")
            .OrderBy(x => x.ScheduledAtUtc).ThenBy(x => x.Id).FirstOrDefault();
        return first is null || first.Id == selectedId ? fee : 0;
    }
}
