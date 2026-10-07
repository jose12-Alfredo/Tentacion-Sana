namespace TentacionSana.Application.Finance;

public interface IIncomeStatementService
{
    Task<IncomeStatementReport> GetAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
}

public sealed record IncomeStatementLine(string Category, decimal Amount);

public sealed record IncomeStatementReport(
    DateOnly From,
    DateOnly To,
    decimal SalesRevenue,
    decimal OtherIncome,
    decimal CostOfSales,
    decimal OtherDirectCosts,
    IReadOnlyList<IncomeStatementLine> OperatingExpenses,
    decimal InventoryPurchases,
    decimal Investments,
    int SalesWithoutCalculatedCost)
{
    public IReadOnlyList<IncomeStatementLine> IncomeAccounts { get; init; } = [];
    public IReadOnlyList<IncomeStatementLine> DirectCostAccounts { get; init; } = [];
    public IReadOnlyList<IncomeStatementLine> InvestmentAccounts { get; init; } = [];
    public decimal TotalRevenue => SalesRevenue + OtherIncome;
    public decimal TotalCosts => CostOfSales + OtherDirectCosts;
    public decimal GrossProfit => TotalRevenue - TotalCosts;
    public decimal TotalOperatingExpenses => OperatingExpenses.Sum(x => x.Amount);
    public decimal NetIncome => GrossProfit - TotalOperatingExpenses;
    public decimal GrossMargin => TotalRevenue == 0 ? 0 : decimal.Round(GrossProfit / TotalRevenue * 100, 1);
    public decimal NetMargin => TotalRevenue == 0 ? 0 : decimal.Round(NetIncome / TotalRevenue * 100, 1);
}
