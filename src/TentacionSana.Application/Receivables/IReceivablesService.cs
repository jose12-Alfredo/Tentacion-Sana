namespace TentacionSana.Application.Receivables;

public interface IReceivablesService
{
    Task<ReceivablesOverview> GetOverviewAsync(CancellationToken cancellationToken = default);
    Task<PayerAccountHistory?> GetHistoryAsync(Guid responsiblePartyId, CancellationToken cancellationToken = default);
    Task<PayerPaymentResult> RegisterPaymentAsync(RegisterPayerPaymentCommand command, Guid userId, CancellationToken cancellationToken = default);
    Task<AccountStatementResult> GenerateStatementAsync(GenerateAccountStatementCommand command, Guid userId, CancellationToken cancellationToken = default);
}

public sealed record ReceivablesOverview(
    decimal TotalPending,
    int ResponsiblePartiesWithDebt,
    int PartiallyPaidOrders,
    int OverdueOrders,
    IReadOnlyList<PayerAccountItem> ResponsibleParties);

public sealed record PayerAccountItem(
    Guid ResponsiblePartyId,
    string Name,
    string? ContactName,
    string Phone,
    IReadOnlyList<string> Customers,
    decimal Total,
    decimal Paid,
    decimal Balance,
    IReadOnlyList<ReceivableOrderItem> Orders);

public sealed record ReceivableOrderItem(
    Guid OrderId,
    long OrderNumber,
    string Customer,
    string Branch,
    DateTimeOffset? DeliveredAtUtc,
    decimal Total,
    decimal Paid,
    decimal Balance,
    int Version,
    int AgeDays,
    string Status,
    int DeliveryEvidenceCount,
    int PaymentEvidenceCount);

public sealed record ManualPaymentAllocation(Guid OrderId, decimal Amount);

public sealed record RegisterPayerPaymentCommand(
    Guid ResponsiblePartyId,
    string IdempotencyKey,
    decimal Amount,
    string Method,
    DateTimeOffset PaymentDateUtc,
    string? Notes,
    bool CollectedByCurrentUser,
    bool ApplyAutomatically,
    IReadOnlyList<ManualPaymentAllocation> Allocations,
    Stream EvidenceContent,
    string EvidenceFileName,
    string EvidenceContentType,
    long EvidenceLength);

public sealed record AppliedPaymentOrder(Guid OrderId, long OrderNumber, decimal Amount);

public sealed record PayerPaymentResult(
    bool Succeeded,
    Guid? PaymentId,
    decimal PreviousBalance,
    decimal Amount,
    decimal CurrentBalance,
    IReadOnlyList<AppliedPaymentOrder> Applications,
    IReadOnlyList<string> Errors);

public sealed record GenerateAccountStatementCommand(
    Guid ResponsiblePartyId,
    string ReportType,
    bool IncludeDeliveryEvidence,
    bool IncludePaymentEvidence,
    bool IncludePartialPayments,
    bool IncludePaidOrders,
    Guid? PaymentId = null,
    IReadOnlyList<Guid>? SelectedOrderIds = null);

public sealed record AccountStatementResult(
    bool Succeeded,
    Guid? ReportId,
    string? ReportNumber,
    string? FileName,
    byte[]? Content,
    IReadOnlyList<string> Errors);

public sealed record PayerAccountHistory(
    Guid ResponsiblePartyId,
    string Name,
    IReadOnlyList<PayerPaymentHistoryItem> Payments,
    IReadOnlyList<AccountStatementHistoryItem> Reports);

public sealed record PayerPaymentHistoryItem(
    Guid Id,
    decimal Amount,
    string Method,
    DateTimeOffset PaymentDateUtc,
    string? Notes,
    Guid? EvidenceId,
    IReadOnlyList<AppliedPaymentOrder> Applications);

public sealed record AccountStatementHistoryItem(
    Guid Id,
    string ReportNumber,
    string Type,
    decimal BalanceAtGeneration,
    DateTimeOffset GeneratedAtUtc,
    int OrderCount);
