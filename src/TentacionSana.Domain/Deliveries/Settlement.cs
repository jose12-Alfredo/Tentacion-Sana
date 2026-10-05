namespace TentacionSana.Domain.Deliveries;

public enum SettlementStatus { Pending, Partial, Settled, WithDifference, Voided }

public sealed class SettlementObligation
{
    private SettlementObligation() { }
    public Guid Id { get; private set; }
    public Guid PaymentId { get; private set; }
    public Guid HolderUserId { get; private set; }
    public decimal Amount { get; private set; }
    public decimal SettledAmount { get; private set; }
    public decimal PendingAmount => Math.Max(0, Amount - SettledAmount);
    public SettlementStatus Status { get; private set; }
    public int Version { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static SettlementObligation Create(Guid paymentId, Guid holderUserId, decimal amount, DateTimeOffset now)
    {
        if (amount <= 0) throw new ArgumentException("El monto pendiente de rendición debe ser positivo.");
        return new() { Id = Guid.NewGuid(), PaymentId = paymentId, HolderUserId = holderUserId, Amount = decimal.Round(amount, 2), Status = SettlementStatus.Pending, Version = 1, CreatedAtUtc = now };
    }

    public void Apply(decimal amount, bool hasDifference)
    {
        amount = decimal.Round(amount, 2);
        if (amount <= 0) throw new ArgumentException("El monto rendido debe ser positivo.");
        if (amount > PendingAmount) throw new InvalidOperationException("La rendición supera el monto pendiente.");
        SettledAmount += amount;
        Status = hasDifference ? SettlementStatus.WithDifference : PendingAmount == 0 ? SettlementStatus.Settled : SettlementStatus.Partial;
        Version++;
    }
}

public sealed class Settlement
{
    private Settlement() { }
    public Guid Id { get; private set; }
    public Guid HolderUserId { get; private set; }
    public Guid ReceivedByUserId { get; private set; }
    public decimal DeclaredAmount { get; private set; }
    public decimal ReceivedAmount { get; private set; }
    public decimal Difference => decimal.Round(DeclaredAmount - ReceivedAmount, 2);
    public SettlementStatus Status { get; private set; }
    public string? DifferenceReason { get; private set; }
    public string? Reference { get; private set; }
    public DateTimeOffset ReceivedAtUtc { get; private set; }
    public List<SettlementAllocation> Allocations { get; private set; } = [];

    public static Settlement Create(Guid holderUserId, Guid receivedByUserId, decimal receivedAmount, string? differenceReason, string? reference, DateTimeOffset now, IEnumerable<(SettlementObligation Obligation, decimal Amount)> allocations)
    {
        var selected = allocations.ToList();
        if (selected.Count == 0) throw new ArgumentException("Selecciona al menos un pago pendiente.");
        var declared = decimal.Round(selected.Sum(x => x.Amount), 2);
        receivedAmount = decimal.Round(receivedAmount, 2);
        if (declared <= 0 || receivedAmount < 0) throw new ArgumentException("Los montos de la rendición no son válidos.");
        if (receivedAmount != declared && string.IsNullOrWhiteSpace(differenceReason)) throw new ArgumentException("Una diferencia exige un motivo.");
        var settlement = new Settlement { Id = Guid.NewGuid(), HolderUserId = holderUserId, ReceivedByUserId = receivedByUserId, DeclaredAmount = declared, ReceivedAmount = receivedAmount, DifferenceReason = Clean(differenceReason), Reference = Clean(reference), ReceivedAtUtc = now, Status = receivedAmount == declared ? SettlementStatus.Settled : SettlementStatus.WithDifference };
        foreach (var item in selected)
        {
            if (item.Obligation.HolderUserId != holderUserId) throw new InvalidOperationException("Todos los pagos deben pertenecer al mismo colaborador.");
            item.Obligation.Apply(item.Amount, receivedAmount != declared);
            settlement.Allocations.Add(SettlementAllocation.Create(settlement.Id, item.Obligation.Id, item.Amount));
        }
        return settlement;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class SettlementAllocation
{
    private SettlementAllocation() { }
    public Guid Id { get; private set; }
    public Guid SettlementId { get; private set; }
    public Guid ObligationId { get; private set; }
    public decimal Amount { get; private set; }
    internal static SettlementAllocation Create(Guid settlementId, Guid obligationId, decimal amount) => new() { Id = Guid.NewGuid(), SettlementId = settlementId, ObligationId = obligationId, Amount = decimal.Round(amount, 2) };
}
