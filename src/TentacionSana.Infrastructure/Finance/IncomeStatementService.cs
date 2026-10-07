using Microsoft.EntityFrameworkCore;
using TentacionSana.Application.Finance;
using TentacionSana.Domain.Finance;
using TentacionSana.Infrastructure.Persistence;

namespace TentacionSana.Infrastructure.Finance;

public sealed class IncomeStatementService(ApplicationDbContext db, TimeProvider clock) : IIncomeStatementService
{
    public async Task<IncomeStatementReport> GetAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        if (endDate < startDate) (startDate, endDate) = (endDate, startDate);
        var offset = clock.GetLocalNow().Offset;
        var fromUtc = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue), offset).ToUniversalTime();
        var toUtc = new DateTimeOffset(endDate.AddDays(1).ToDateTime(TimeOnly.MinValue), offset).ToUniversalTime();

        var sales = await db.Sales.AsNoTracking()
            .Where(x => x.OccurredAtUtc >= fromUtc && x.OccurredAtUtc < toUtc
                && db.Orders.Any(order => order.Id == x.OrderId && order.ArchivedAtUtc == null))
            .Select(x => new { x.Amount, x.HistoricalCost })
            .ToListAsync(cancellationToken);

        var manualMovements = await db.CashMovements.AsNoTracking()
            .Where(x => x.Source == CashSource.Manual && x.OccurredAtUtc >= fromUtc && x.OccurredAtUtc < toUtc)
            .Select(x => new { x.Direction, x.Category, x.Amount, x.AccountingAccountId })
            .ToListAsync(cancellationToken);

        var accountIds = manualMovements.Where(x => x.AccountingAccountId != null)
            .Select(x => x.AccountingAccountId!.Value).Distinct().ToList();
        var accounts = await db.AccountingAccounts.AsNoTracking().Where(x => accountIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => new { x.Code, x.Name, x.Kind }, cancellationToken);

        var inventoryPurchases = await db.CashMovements.AsNoTracking()
            .Where(x => x.Source == CashSource.InventoryPurchase && x.OccurredAtUtc >= fromUtc && x.OccurredAtUtc < toUtc)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;

        var otherIncome = manualMovements
            .Where(x => x.Direction == CashDirection.Income && KindOf(x.AccountingAccountId, x.Category) == AccountingAccountKind.Income)
            .Sum(x => x.Amount);
        var expenses = manualMovements.Where(x => x.Direction == CashDirection.Expense).ToList();
        var directCosts = expenses
            .Where(x => KindOf(x.AccountingAccountId, x.Category) == AccountingAccountKind.DirectCost)
            .Sum(x => x.Amount);
        var investments = expenses
            .Where(x => KindOf(x.AccountingAccountId, x.Category) == AccountingAccountKind.Asset)
            .Sum(x => x.Amount);
        var operatingExpenses = Lines(AccountingAccountKind.OperatingExpense, CashDirection.Expense);

        return new IncomeStatementReport(
            startDate,
            endDate,
            sales.Sum(x => x.Amount),
            otherIncome,
            sales.Sum(x => x.HistoricalCost ?? 0),
            directCosts,
            operatingExpenses,
            inventoryPurchases,
            investments,
            sales.Count(x => x.HistoricalCost is null))
        {
            IncomeAccounts = Lines(AccountingAccountKind.Income, CashDirection.Income),
            DirectCostAccounts = Lines(AccountingAccountKind.DirectCost, CashDirection.Expense),
            InvestmentAccounts = Lines(AccountingAccountKind.Asset, CashDirection.Expense)
        };

        List<IncomeStatementLine> Lines(AccountingAccountKind kind, CashDirection direction) => manualMovements
            .Where(x => x.Direction == direction && KindOf(x.AccountingAccountId, x.Category) == kind)
            .GroupBy(x => AccountLabel(x.AccountingAccountId, x.Category))
            .Select(x => new IncomeStatementLine(x.Key, x.Sum(y => y.Amount)))
            .OrderBy(x => x.Category)
            .ToList();

        string AccountLabel(Guid? accountId, string category) =>
            accountId is Guid id && accounts.TryGetValue(id, out var account)
                ? $"{account.Code} · {account.Name}"
                : category;

        AccountingAccountKind KindOf(Guid? accountId, string category)
        {
            if (accountId is not null && accounts.TryGetValue(accountId.Value, out var account)) return account.Kind;
            if (AccountingCategories.IsOtherIncome(category)) return AccountingAccountKind.Income;
            return AccountingCategories.ClassifyExpense(category) switch
            {
                AccountingCategoryKind.DirectCost => AccountingAccountKind.DirectCost,
                AccountingCategoryKind.Investment => AccountingAccountKind.Asset,
                _ => AccountingAccountKind.OperatingExpense
            };
        }
    }
}
