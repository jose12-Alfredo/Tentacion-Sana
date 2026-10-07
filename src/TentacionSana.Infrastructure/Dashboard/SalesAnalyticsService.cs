using Microsoft.EntityFrameworkCore;
using TentacionSana.Application.Dashboard;
using TentacionSana.Domain.Orders;
using TentacionSana.Infrastructure.Persistence;

namespace TentacionSana.Infrastructure.Dashboard;

public sealed class SalesAnalyticsService(ApplicationDbContext db, TimeProvider clock) : ISalesAnalyticsService
{
    public async Task<SalesAnalyticsReport> GetAsync(DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken = default)
    {
        if (fromDate > toDate) throw new ArgumentException("La fecha inicial debe ser anterior a la fecha final.");

        var offset = clock.GetLocalNow().Offset;
        DateTimeOffset? fromUtc = fromDate is { } first
            ? new DateTimeOffset(first.ToDateTime(TimeOnly.MinValue), offset).ToUniversalTime() : null;
        DateTimeOffset? untilUtc = toDate is { } last
            ? new DateTimeOffset(last.AddDays(1).ToDateTime(TimeOnly.MinValue), offset).ToUniversalTime() : null;

        var totalCustomers = await db.Customers.AsNoTracking().CountAsync(cancellationToken);
        var orders = await db.Orders.AsNoTracking()
            .Where(x => x.Status == OrderStatus.Delivered && x.ArchivedAtUtc == null
                && x.StatusHistory.Any(h => h.NewStatus == OrderStatus.Delivered
                    && (fromUtc == null || h.ChangedAtUtc >= fromUtc)
                    && (untilUtc == null || h.ChangedAtUtc < untilUtc)))
            .Include(x => x.StatusHistory)
            .Include(x => x.Lines)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var customerIds = orders.Select(x => x.CustomerId).Distinct().ToList();
        var customerNames = await db.Customers.AsNoTracking()
            .Where(x => customerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var sales = orders.Select(order => new
        {
            Order = order,
            Date = DateOnly.FromDateTime(order.StatusHistory
                .Where(h => h.NewStatus == OrderStatus.Delivered)
                .Max(h => h.ChangedAtUtc).ToOffset(offset).DateTime),
            Customer = customerNames.GetValueOrDefault(order.CustomerId, "Cliente sin nombre"),
            Revenue = order.Lines.Where(line => line.IsActive).Sum(line => line.LineTotal),
            Units = order.Lines.Where(line => line.IsActive).Sum(line => line.SaleQuantity)
        }).ToList();

        var customers = sales.GroupBy(x => x.Order.CustomerId)
            .Select(g => new CustomerSales(g.Key, g.First().Customer, g.Sum(x => x.Revenue), g.Count(), g.Sum(x => x.Units)))
            .OrderByDescending(x => x.Revenue).ThenBy(x => x.Name).ToList();
        var products = orders.SelectMany(order => order.Lines.Where(line => line.IsActive && line.SaleQuantity > 0))
            .GroupBy(line => line.ProductId)
            .Select(g => new ProductSales(g.Key, g.First().ProductName, g.Sum(x => x.LineTotal), g.Sum(x => x.SaleQuantity)))
            .OrderByDescending(x => x.Revenue).ThenBy(x => x.Name).ToList();
        var daily = sales.GroupBy(x => x.Date)
            .Select(g => new SalesPoint(g.Key, g.Sum(x => x.Revenue), g.Count()))
            .OrderBy(x => x.Date).ToList();
        var recent = sales.OrderByDescending(x => x.Date).ThenByDescending(x => x.Order.Number)
            .Take(12).Select(x => new DeliveredSale(x.Order.Id, x.Order.Number, x.Date, x.Customer, x.Revenue)).ToList();

        return new SalesAnalyticsReport(totalCustomers, customers.Count, orders.Count,
            sales.Sum(x => x.Units), sales.Sum(x => x.Revenue), daily, customers, products, recent);
    }
}
