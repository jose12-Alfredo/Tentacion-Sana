namespace TentacionSana.Domain.Finance;

public enum AccountingAccountKind
{
    Income,
    DirectCost,
    OperatingExpense,
    Asset,
    Liability,
    Equity
}

public sealed class AccountingAccount
{
    private AccountingAccount() { }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public AccountingAccountKind Kind { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    public static AccountingAccount Create(string code, string name, AccountingAccountKind kind, Guid userId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 20)
            throw new ArgumentException("El código de la cuenta es obligatorio y admite hasta 20 caracteres.");
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120)
            throw new ArgumentException("El nombre de la cuenta es obligatorio y admite hasta 120 caracteres.");
        return new AccountingAccount
        {
            Id = Guid.NewGuid(), Code = code.Trim().ToUpperInvariant(), Name = name.Trim(), Kind = kind,
            IsActive = true, CreatedAtUtc = now, CreatedByUserId = userId
        };
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}
