namespace TentacionSana.Application.Dashboard;

public interface ISalesAnalyticsService
{
    Task<SalesAnalyticsReport> GetAsync(DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken = default);
}

public sealed record SalesAnalyticsReport(
    int TotalCustomers,
    int BuyingCustomers,
    int DeliveredOrders,
    int UnitsSold,
    decimal Revenue,
    IReadOnlyList<SalesPoint> DailySales,
    IReadOnlyList<CustomerSales> Customers,
    IReadOnlyList<ProductSales> Products,
    IReadOnlyList<DeliveredSale> RecentSales)
{
    public IReadOnlyList<BranchSales> Branches { get; init; } = [];
    public int ReplacementUnits { get; init; }
    public int ReplacementOrders { get; init; }
    public int TastingUnits { get; init; }
    public int TastingOrders { get; init; }
}

public sealed record SalesPoint(DateOnly Date, decimal Revenue, int Orders);
public sealed record CustomerSales(Guid Id, string Name, decimal Revenue, int Orders, int Units);
public sealed record BranchSales(Guid CustomerId, Guid? DeliveryPointId, string Customer, string Branch, decimal Revenue, int Orders, int Units, int ReplacementUnits, int TastingUnits);
public sealed record ProductSales(Guid Id, string Name, decimal Revenue, int Units);
public sealed record DeliveredSale(Guid Id, long Number, DateOnly Date, string Customer, decimal Revenue);
