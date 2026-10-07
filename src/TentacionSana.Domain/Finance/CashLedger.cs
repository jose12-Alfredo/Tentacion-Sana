namespace TentacionSana.Domain.Finance;

public enum CashAccount { Bank, Cash }
public enum CashDirection { Income, Expense }
public enum CashSource { CustomerPayment, InventoryPurchase, Manual, Transfer, CashCountAdjustment }

public sealed class CashMovement
{
    private CashMovement() { }
    public Guid Id { get; private set; }
    public CashAccount Account { get; private set; }
    public CashDirection Direction { get; private set; }
    public CashSource Source { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string Detail { get; private set; } = "";
    public string Category { get; private set; } = "";
    public decimal Amount { get; private set; }
    public Guid? PaymentId { get; private set; }
    public Guid? SupplyPurchaseId { get; private set; }
    public Guid? TransferId { get; private set; }
    public Guid? AccountingAccountId { get; private set; }
    public string EvidencePublicId { get; private set; } = "";
    public string EvidenceFileName { get; private set; } = "";
    public string EvidenceFormat { get; private set; } = "";
    public long EvidenceBytes { get; private set; }
    public Guid RegisteredByUserId { get; private set; }
    public DateTimeOffset RegisteredAtUtc { get; private set; }

    public static CashMovement Create(CashAccount account, CashDirection direction, CashSource source, DateTimeOffset occurredAtUtc,
        string detail, decimal amount, string publicId, string fileName, string format, long bytes, Guid userId,
        DateTimeOffset registeredAtUtc, Guid? paymentId = null, Guid? supplyPurchaseId = null, string category = "", Guid? transferId = null,
        Guid? accountingAccountId = null)
    {
        if (string.IsNullOrWhiteSpace(detail)) throw new ArgumentException("El detalle es obligatorio.");
        if (amount <= 0) throw new ArgumentException("El monto debe ser mayor que cero.");
        if (string.IsNullOrWhiteSpace(publicId)) throw new ArgumentException("La foto del respaldo es obligatoria.");
        return new CashMovement { Id = Guid.NewGuid(), Account = account, Direction = direction, Source = source,
            OccurredAtUtc = occurredAtUtc, Detail = detail.Trim(), Category = category.Trim(), Amount = decimal.Round(amount, 2), PaymentId = paymentId, TransferId=transferId, AccountingAccountId=accountingAccountId,
            SupplyPurchaseId = supplyPurchaseId, EvidencePublicId = publicId, EvidenceFileName = Path.GetFileName(fileName),
            EvidenceFormat = format, EvidenceBytes = bytes, RegisteredByUserId = userId, RegisteredAtUtc = registeredAtUtc };
    }

    public void CorrectManual(CashAccount account, CashDirection direction, DateTimeOffset occurredAtUtc,
        string detail, decimal amount, Guid accountingAccountId, string category)
    {
        if (Source != CashSource.Manual) throw new InvalidOperationException("Solo se pueden corregir movimientos manuales desde caja.");
        ValidateCorrection(detail, amount);
        if (accountingAccountId == Guid.Empty || string.IsNullOrWhiteSpace(category)) throw new ArgumentException("Selecciona una cuenta contable.");
        Account = account;
        Direction = direction;
        OccurredAtUtc = occurredAtUtc;
        Detail = detail.Trim();
        Amount = decimal.Round(amount, 2);
        AccountingAccountId = accountingAccountId;
        Category = category.Trim();
    }

    public void CorrectTransfer(CashAccount account, CashDirection direction, DateTimeOffset occurredAtUtc,
        string detail, decimal amount)
    {
        if (Source != CashSource.Transfer || TransferId is null) throw new InvalidOperationException("El movimiento no pertenece a una transferencia.");
        ValidateCorrection(detail, amount);
        Account = account;
        Direction = direction;
        OccurredAtUtc = occurredAtUtc;
        Detail = detail.Trim();
        Amount = decimal.Round(amount, 2);
    }

    private static void ValidateCorrection(string detail, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(detail)) throw new ArgumentException("El detalle es obligatorio.");
        if (detail.Trim().Length > 500) throw new ArgumentException("El detalle no puede superar 500 caracteres.");
        if (amount <= 0 || decimal.Round(amount, 2) <= 0) throw new ArgumentException("El monto debe ser mayor que cero.");
    }
}

public sealed class CashCount
{
    private CashCount() { }
    public Guid Id { get; private set; } public DateTimeOffset CountedAtUtc { get; private set; }
    public decimal ExpectedAmount { get; private set; } public decimal CountedAmount { get; private set; }
    public decimal Difference { get; private set; } public string Observation { get; private set; }="";
    public string? EvidencePublicId { get; private set; } public string? EvidenceFileName { get; private set; }
    public string? EvidenceFormat { get; private set; } public long EvidenceBytes { get; private set; }
    public Guid RegisteredByUserId { get; private set; }
    public static CashCount Create(DateTimeOffset at,decimal expected,decimal counted,string observation,string? publicId,string? fileName,string? format,long bytes,Guid userId)
    {
        if(counted<0)throw new ArgumentException("El efectivo contado no puede ser negativo.");
        var difference=decimal.Round(counted-expected,2);
        if(difference!=0&&string.IsNullOrWhiteSpace(observation))throw new ArgumentException("Explica la diferencia encontrada.");
        if(difference!=0&&string.IsNullOrWhiteSpace(publicId))throw new ArgumentException("Adjunta una foto cuando exista diferencia en caja.");
        return new(){Id=Guid.NewGuid(),CountedAtUtc=at,ExpectedAmount=expected,CountedAmount=counted,Difference=difference,Observation=observation?.Trim()??"",EvidencePublicId=publicId,EvidenceFileName=fileName is null?null:Path.GetFileName(fileName),EvidenceFormat=format,EvidenceBytes=bytes,RegisteredByUserId=userId};
    }
}

public enum PayableStatus { Pending, Paid }
public sealed class CashPayable
{
    private CashPayable() { }
    public Guid Id { get; private set; }
    public Guid SupplyPurchaseId { get; private set; }
    public string PersonName { get; private set; } = "";
    public decimal Amount { get; private set; }
    public PayableStatus Status { get; private set; }
    public string EvidencePublicId { get; private set; } = "";
    public string EvidenceFileName { get; private set; } = "";
    public string EvidenceFormat { get; private set; } = "";
    public long EvidenceBytes { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public static CashPayable Create(Guid purchaseId, string person, decimal amount, string publicId, string fileName, string format, long bytes, Guid userId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(person)) throw new ArgumentException("Indica la persona que realizó la rendición.");
        if (amount <= 0) throw new ArgumentException("El monto de la rendición debe ser mayor que cero.");
        if (string.IsNullOrWhiteSpace(publicId)) throw new ArgumentException("La foto de la rendición es obligatoria.");
        return new() { Id = Guid.NewGuid(), SupplyPurchaseId = purchaseId, PersonName = person.Trim(), Amount = decimal.Round(amount, 2), Status = PayableStatus.Pending, EvidencePublicId=publicId,EvidenceFileName=Path.GetFileName(fileName),EvidenceFormat=format,EvidenceBytes=bytes,CreatedAtUtc = now, CreatedByUserId = userId };
    }
}
