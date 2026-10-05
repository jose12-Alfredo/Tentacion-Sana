namespace TentacionSana.Application.Finance;

public interface ICashLedgerService
{
    Task<CashDashboard> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<CashResult> RegisterManualAsync(ManualCashMovementCommand command, Guid userId, CancellationToken cancellationToken = default);
    Task<CashResult> TransferAsync(TransferCashCommand command, Guid userId, CancellationToken cancellationToken = default);
    Task<CashResult> RegisterCashCountAsync(CashCountCommand command, Guid userId, CancellationToken cancellationToken = default);
    Task<CashEvidenceResult> GetEvidenceUrlAsync(Guid movementId, Guid userId, CancellationToken cancellationToken = default);
    Task<CashEvidenceContentResult> GetEvidenceContentAsync(Guid movementId, Guid userId, CancellationToken cancellationToken = default);
}

public sealed record ManualCashMovementCommand(string Account, string Direction, Guid AccountingAccountId, DateTimeOffset OccurredAtUtc, string Detail, decimal Amount,
    Stream EvidenceContent, string EvidenceFileName, string EvidenceContentType, long EvidenceLength);
public sealed record TransferCashCommand(string FromAccount,string ToAccount,DateTimeOffset OccurredAtUtc,string Detail,decimal Amount,Stream EvidenceContent,string EvidenceFileName,string EvidenceContentType,long EvidenceLength);
public sealed record CashCountCommand(DateTimeOffset CountedAtUtc,decimal CountedAmount,string Observation,Stream? EvidenceContent,string? EvidenceFileName,string? EvidenceContentType,long EvidenceLength);
public sealed record CashResult(bool Succeeded, Guid? Id, IReadOnlyList<string> Errors);
public sealed record CashEvidenceResult(bool Succeeded, string? Url, IReadOnlyList<string> Errors);
public sealed record CashEvidenceContentResult(bool Succeeded, byte[]? Content, string? ContentType, IReadOnlyList<string> Errors);
public sealed record CashMovementItem(Guid Id, string Account, string Direction, string Source, string Category, DateTimeOffset OccurredAtUtc, string Detail, decimal Amount,Guid? TransferId);
public sealed record CashPayableItem(Guid Id, string Person, decimal Amount, DateTimeOffset CreatedAtUtc, string Status);
public sealed record CashDashboard(decimal BankBalance, decimal CashBalance, decimal TotalIncome, decimal TotalExpense,decimal NetFlow,
    IReadOnlyList<CashMovementItem> Movements, IReadOnlyList<CashPayableItem> Payables);
