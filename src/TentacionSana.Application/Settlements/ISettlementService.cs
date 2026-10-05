namespace TentacionSana.Application.Settlements;

public interface ISettlementService
{
    Task<IReadOnlyList<SettlementHolderSummary>> GetPendingAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SettlementHistoryItem>> GetHistoryAsync(CancellationToken cancellationToken = default);
    Task<SettlementResult> RegisterAsync(RegisterSettlementCommand command, Guid userId, CancellationToken cancellationToken = default);
}

public sealed record SettlementObligationItem(Guid Id, Guid PaymentId, long OrderNumber, string Customer, decimal Amount, decimal Settled, decimal Pending, int Version, DateTimeOffset ReceivedAtUtc);
public sealed record SettlementHolderSummary(Guid HolderUserId, string HolderName, decimal Received, decimal Settled, decimal Pending, IReadOnlyList<SettlementObligationItem> Obligations);
public sealed record SettlementHistoryItem(Guid Id, string HolderName, string ReceiverName, decimal Declared, decimal Received, decimal Difference, string Status, string? DifferenceReason, string? Reference, DateTimeOffset ReceivedAtUtc);
public sealed record SettlementAllocationCommand(Guid ObligationId, int ExpectedVersion, decimal Amount);
public sealed record RegisterSettlementCommand(Guid HolderUserId, string IdempotencyKey, decimal ReceivedAmount, string? DifferenceReason, string? Reference, IReadOnlyList<SettlementAllocationCommand> Allocations);
public sealed record SettlementResult(bool Succeeded, Guid? Id, IReadOnlyList<string> Errors, bool IsConflict = false);
