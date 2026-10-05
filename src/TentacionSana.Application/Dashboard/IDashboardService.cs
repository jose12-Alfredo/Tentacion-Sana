namespace TentacionSana.Application.Dashboard;

public interface IDashboardService { Task<DashboardSummary> GetAsync(CancellationToken cancellationToken = default); }
public sealed record DashboardSummary(int OpenOrders, int LateOrders, int ProductionShortageUnits, decimal Sales, decimal Collected, decimal Receivable, decimal PendingSettlement, IReadOnlyList<DashboardSettlementItem> Settlements);
public sealed record DashboardSettlementItem(Guid HolderUserId, string HolderName, decimal Pending);
