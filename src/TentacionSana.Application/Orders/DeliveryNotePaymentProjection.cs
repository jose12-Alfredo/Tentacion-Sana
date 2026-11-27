namespace TentacionSana.Application.Orders;

// Representación documental: nunca aplica pagos ni atribuye saldos generales a una salida.
public static class DeliveryNotePaymentProjection
{
    public static (string? Label, string? Warning) Evaluate(bool show, decimal documentTotal, decimal orderTotal,
        decimal confirmedAllocatedPayments, bool coversWholeOrder, decimal? receivablePaid)
    {
        if (!show || documentTotal <= 0) return (null, null);
        if (confirmedAllocatedPayments < 0 || orderTotal <= 0 ||
            (receivablePaid is { } paid && paid != confirmedAllocatedPayments))
            return (null, "Se omitió el estado de pago: la información de cobranza no permite verificar el importe cubierto de este documento.");
        if (confirmedAllocatedPayments >= orderTotal) return (null, null);
        if (confirmedAllocatedPayments == 0) return ("PENDIENTE DE PAGO", null);
        if (coversWholeOrder) return ("PENDIENTE DE PAGO", null);
        return (null, "Se omitió el estado de pago: los cobros están aplicados al pedido y no permiten atribuir con fiabilidad el saldo a esta entrega parcial.");
    }
}
