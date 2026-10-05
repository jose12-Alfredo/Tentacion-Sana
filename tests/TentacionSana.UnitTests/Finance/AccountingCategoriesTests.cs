using TentacionSana.Application.Finance;

namespace TentacionSana.UnitTests.Finance;

public sealed class AccountingCategoriesTests
{
    [Theory]
    [InlineData("Ingredientes", AccountingCategoryKind.DirectCost)]
    [InlineData("Envases", AccountingCategoryKind.DirectCost)]
    [InlineData("Pasajes", AccountingCategoryKind.OperatingExpense)]
    [InlineData("Servicios", AccountingCategoryKind.OperatingExpense)]
    [InlineData("Utensilios menores", AccountingCategoryKind.OperatingExpense)]
    [InlineData("Equipos de cocina", AccountingCategoryKind.Investment)]
    public void ExpenseCategoriesHaveTheExpectedAccountingTreatment(string category, AccountingCategoryKind expected)
    {
        Assert.Equal(expected, AccountingCategories.ClassifyExpense(category));
    }

    [Fact]
    public void IncomeStatementCalculatesGrossAndNetProfit()
    {
        var report = new IncomeStatementReport(
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31),
            1_000, 50, 400, 50,
            [new IncomeStatementLine("Pasajes", 100), new IncomeStatementLine("Servicios", 50)],
            300, 200, 0);

        Assert.Equal(1_050, report.TotalRevenue);
        Assert.Equal(450, report.TotalCosts);
        Assert.Equal(600, report.GrossProfit);
        Assert.Equal(150, report.TotalOperatingExpenses);
        Assert.Equal(450, report.NetIncome);
    }
}
