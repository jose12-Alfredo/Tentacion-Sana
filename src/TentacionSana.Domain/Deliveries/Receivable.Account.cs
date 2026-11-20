namespace TentacionSana.Domain.Deliveries;

public sealed partial class Receivable
{
    public void ReassignUnpaidAccount(Guid payerId)
    {
        if (payerId == Guid.Empty) throw new ArgumentException("El responsable de pago es obligatorio.");
        if (PayerId == payerId) return;
        if (PaidAmount != 0) throw new InvalidOperationException("Una cuenta con pagos registrados conserva su responsable histórico.");
        PayerId = payerId;
        Version++;
    }
}
