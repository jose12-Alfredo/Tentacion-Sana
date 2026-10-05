using Microsoft.EntityFrameworkCore;
using TentacionSana.Application.Dashboard;
using TentacionSana.Domain.Orders;
using TentacionSana.Infrastructure.Persistence;

namespace TentacionSana.Infrastructure.Dashboard;

public sealed class DashboardService(ApplicationDbContext db) : IDashboardService
{
    public async Task<DashboardSummary> GetAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var closed = new[] { OrderStatus.Delivered, OrderStatus.Cancelled };
        var open = await db.Orders.CountAsync(x => !closed.Contains(x.Status), cancellationToken);
        var late = await db.Orders.CountAsync(x => !closed.Contains(x.Status) && x.PromisedAtUtc < now, cancellationToken);
        var shortage = await db.StockReservations.Where(x => x.IsActive).SumAsync(x => (int?)x.ShortageQuantity, cancellationToken) ?? 0;
        var sales = await db.Sales.SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;
        var collected = await db.Payments.Where(x => x.Status == Domain.Deliveries.PaymentStatus.Confirmed).SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;
        var invoiced = await db.Receivables.SumAsync(x => (decimal?)x.InvoicedAmount, cancellationToken) ?? 0;
        var paid = await db.Receivables.SumAsync(x => (decimal?)x.PaidAmount, cancellationToken) ?? 0;
        var pendingRows = await db.SettlementObligations.Where(x => x.SettledAmount < x.Amount).GroupBy(x => x.HolderUserId).Select(x => new { Id = x.Key, Pending = x.Sum(y => y.Amount - y.SettledAmount) }).ToListAsync(cancellationToken);
        var ids = pendingRows.Select(x => x.Id).ToList();
        var names = await db.Users.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);
        var settlements = pendingRows.Select(x => new DashboardSettlementItem(x.Id, names.GetValueOrDefault(x.Id, "Colaborador"), x.Pending)).OrderByDescending(x => x.Pending).ToList();
        return new(open, late, shortage, sales, collected, Math.Max(0, invoiced - paid), settlements.Sum(x => x.Pending), settlements);
    }
}
