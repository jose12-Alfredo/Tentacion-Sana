using TentacionSana.Domain.Inventory;
namespace TentacionSana.UnitTests.Inventory;
public sealed class InventoryTests
{
 [Fact]public void BalanceSeparatesPhysicalReservedAndAvailable(){var x=ProductStockBalance.Create(Guid.NewGuid());x.Produce(10);Assert.Equal(6,x.Reserve(6));Assert.Equal(10,x.PhysicalQuantity);Assert.Equal(6,x.ReservedQuantity);Assert.Equal(4,x.AvailableQuantity);}
 [Fact]public void BalanceNeverRemovesReservedStock(){var x=ProductStockBalance.Create(Guid.NewGuid());x.Produce(5);x.Reserve(4);Assert.Throws<InvalidOperationException>(()=>x.Remove(2));}
 [Fact]public void BatchFreezesEstimatedCostAndConsumption(){var x=ProductionBatch.Create("L-1",Guid.NewGuid(),Guid.NewGuid(),DateTimeOffset.UtcNow,10,2,3.25m,Guid.NewGuid());x.Consume(4);Assert.Equal(6,x.RemainingUnits);Assert.Equal(32.50m,x.EstimatedTotalCost);}
 [Fact]public void StockCountKeepsExpectedCountedAndDifference(){var x=StockCount.Create(Guid.NewGuid(),10,8,"Conteo",Guid.NewGuid(),DateTimeOffset.UtcNow);Assert.Equal(-2,x.Difference);}
 [Fact]public void AdministratorCorrectionPreservesConsumedUnits(){var x=ProductionBatch.Create("L-2",Guid.NewGuid(),null,DateTimeOffset.UtcNow,10,1,null,Guid.NewGuid());x.Consume(4);Assert.Throws<InvalidOperationException>(()=>x.Correct(3,0));Assert.Equal(-2,x.Correct(8,1));Assert.Equal(4,x.RemainingUnits);}
 [Fact]public void VoidingBatchKeepsHistoryAndRemovesRemainingUnits(){var user=Guid.NewGuid();var x=ProductionBatch.Create("L-3",Guid.NewGuid(),null,DateTimeOffset.UtcNow,10,0,null,user);x.Consume(3);var removed=x.Void(user,DateTimeOffset.UtcNow,"Registro equivocado");Assert.Equal(7,removed);Assert.True(x.IsVoided);Assert.Equal(0,x.RemainingUnits);Assert.Equal(10,x.GoodUnits);}
 [Fact]public void PurchaseConvertsPackagesToRecipeUnit(){var purchase=SupplyPurchase.Create(Guid.NewGuid(),DateTimeOffset.UtcNow,"bolsa de 500 g",2,35,500,Guid.NewGuid());Assert.Equal(1000,purchase.TotalBaseQuantity);Assert.Equal(70,purchase.TotalPrice);Assert.Equal("bolsa de 500 g",purchase.PurchasePresentation);}
 [Fact]public void SupplyBalanceKeepsAVisibleDeficitAfterProduction(){var balance=SupplyBalance.Create(Guid.NewGuid());balance.Add(10);balance.Consume(12.5m);Assert.Equal(-2.5m,balance.Quantity);}
 [Fact]public void RecipeStoresItsBatchYield(){var recipe=RecipeVersion.Create(Guid.NewGuid(),1,"Budín",DateTimeOffset.UtcNow,5);Assert.Equal(5,recipe.YieldQuantity);}
 [Fact]public void SupplyCanUpdateItsFuturePurchaseDefaults(){var now=DateTimeOffset.UtcNow;var supply=Supply.Create("Granola",MeasurementUnit.Gram,"bolsa de 1 kg",1000,now);supply.Update("Granola de avena",MeasurementUnit.Gram,"bolsa de 800 g",800,now.AddMinutes(1));Assert.Equal("Granola de avena",supply.Name);Assert.Equal("bolsa de 800 g",supply.PurchasePresentation);Assert.Equal(800,supply.BaseQuantityPerPackage);}
}
