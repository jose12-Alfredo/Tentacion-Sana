using TentacionSana.Application.Inventory;

namespace TentacionSana.UnitTests.Inventory;

public sealed class ProductionAllocationPlannerTests
{
    private static readonly DateTimeOffset Today = new(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(55, 30, 30, 25)]
    [InlineData(55, 29, 29, 26)]
    [InlineData(20, 50, 20, 0)]
    [InlineData(0, 50, 0, 0)]
    public void AssignsOnlyWhatConfirmedOrdersNeed(int shortage, int available, int expectedAssigned, int expectedShortage)
    {
        var demand = shortage == 0 ? [] : new[] { Demand(shortage, Today) };
        var result = ProductionAllocationPlanner.Plan(available, demand);
        Assert.Equal(expectedAssigned, result.Sum(x => x.Quantity));
        Assert.Equal(expectedShortage, shortage - result.Sum(x => x.Quantity));
        Assert.True(result.All(x => x.Quantity <= shortage));
    }

    [Fact]
    public void PrioritizesNearestPromisedDate()
    {
        var first = Demand(10, Today);
        var second = Demand(20, Today.AddDays(1));
        var result = ProductionAllocationPlanner.Plan(15, [second, first]);
        Assert.Equal(first.OrderId, result[0].OrderId);
        Assert.Equal(10, result[0].Quantity);
        Assert.Equal(5, result[1].Quantity);
    }

    [Fact]
    public void UsesConfirmationAgeWhenPromisedDatesMatch()
    {
        var old = Demand(10, Today, Today.AddHours(-2));
        var recent = Demand(10, Today, Today.AddHours(-1));
        Assert.Equal(old.OrderId, ProductionAllocationPlanner.Plan(5, [recent, old]).Single().OrderId);
    }

    [Fact]
    public void IgnoresUnconfirmedDemand()
    {
        var pending = Demand(20, Today) with { IsConfirmed = false };
        var confirmed = Demand(10, Today.AddDays(1));
        var result = ProductionAllocationPlanner.Plan(10, [pending, confirmed]);
        Assert.Single(result);
        Assert.Equal(confirmed.OrderId, result[0].OrderId);
        Assert.Equal(10, result[0].Quantity);
    }

    [Fact]
    public void CalculatesOnlyUsableUnitsAndRejectsWasteAboveProduction()
    {
        Assert.Equal(29, ProductionQuantities.GoodUnits(30, 1));
        var error = Assert.Throws<ArgumentException>(() => ProductionQuantities.GoodUnits(10, 11));
        Assert.Contains("no puede superar", error.Message);
    }

    [Fact]
    public void ConvertsTheBudinRecipeAndPurchasePresentations()
    {
        Assert.Equal(120m, RecipeQuantities.Required(60m, 5m, 10m));
        Assert.Equal(2m, RecipeQuantities.PackagesToBuy(620m, 500m));
        Assert.Equal(3m, RecipeQuantities.DropsToMilliliters(60m));
    }

    private static ReservationDemand Demand(int shortage, DateTimeOffset promised, DateTimeOffset? confirmed = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), shortage, promised, confirmed ?? Today.AddDays(-1));
}
