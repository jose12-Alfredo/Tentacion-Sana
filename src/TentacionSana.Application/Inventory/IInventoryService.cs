namespace TentacionSana.Application.Inventory;
public interface IInventoryService
{
 Task<InventoryResult> RegisterProductionAsync(ProductionCommand command,Guid userId,CancellationToken cancellationToken=default);
 Task<InventoryResult> RegisterOutputAsync(OutputCommand command,Guid userId,CancellationToken cancellationToken=default);
 Task<InventoryResult> RegisterCountAsync(CountCommand command,Guid userId,CancellationToken cancellationToken=default);
 Task<IReadOnlyList<StockItem>> GetStockAsync(CancellationToken cancellationToken=default);
 Task<IReadOnlyList<BatchItem>> GetBatchesAsync(CancellationToken cancellationToken=default);
 Task<IReadOnlyList<BatchItem>> GetRecentBatchesAsync(int count,CancellationToken cancellationToken=default);
 Task<IReadOnlyList<MovementItem>> GetMovementsAsync(CancellationToken cancellationToken=default);
 Task<ProductionDashboard> GetProductionDashboardAsync(CancellationToken cancellationToken=default);
 Task<IReadOnlyList<ProductionOrderDemand>> GetProductionOrderDemandAsync(Guid productId,CancellationToken cancellationToken=default);
 Task<InventoryResult> UpdateProductionBatchAsync(UpdateProductionBatchCommand command,Guid userId,CancellationToken cancellationToken=default);
 Task<InventoryResult> DeleteProductionBatchAsync(DeleteProductionBatchCommand command,Guid userId,CancellationToken cancellationToken=default);
 Task<SupplyInventoryDashboard> GetSupplyDashboardAsync(CancellationToken cancellationToken=default);
 Task<RecipeDetail?> GetRecipeAsync(Guid productId,CancellationToken cancellationToken=default);
 Task<InventoryResult> CreateSupplyAsync(CreateSupplyCommand command,Guid userId,CancellationToken cancellationToken=default);
 Task<InventoryResult> UpdateSupplyAsync(UpdateSupplyCommand command,Guid userId,CancellationToken cancellationToken=default);
 Task<InventoryResult> RegisterPurchaseAsync(PurchaseCommand command,Guid userId,CancellationToken cancellationToken=default);
 Task<InventoryResult> SaveRecipeAsync(SaveRecipeCommand command,Guid userId,CancellationToken cancellationToken=default);
 Task<InventoryResult> RegisterSupplyCountAsync(SupplyCountCommand command,Guid userId,CancellationToken cancellationToken=default);
}
public sealed record ProductionCommand(Guid ProductId,DateTimeOffset ProducedAtUtc,int ProducedUnits,int WasteUnits);
public sealed record UpdateProductionBatchCommand(Guid BatchId,int Version,int ProducedUnits,int WasteUnits,string Reason);
public sealed record DeleteProductionBatchCommand(Guid BatchId,int Version,string Reason);
public sealed record OutputCommand(Guid ProductId,int Quantity,string Kind,string Reason);
public sealed record CountCommand(Guid ProductId,int CountedQuantity,string Reason);
public sealed record InventoryResult(bool Succeeded,Guid? Id,IReadOnlyList<string> Errors,int AssignedUnits=0,int AssignedOrders=0,IReadOnlyList<string>? Warnings=null);
public sealed record StockItem(Guid ProductId,string Product,int Physical,int Reserved,int Available,int Shortage,int Version);
public sealed record BatchItem(Guid Id,string Number,string Product,DateTimeOffset ProducedAtUtc,int GoodUnits,int WasteUnits,int Remaining,decimal? EstimatedUnitCost,string RegisteredBy,int Version);
public sealed record MovementItem(Guid Id,string Product,string Kind,int Quantity,decimal? HistoricalTotalCost,string Reason,DateTimeOffset AtUtc);
public sealed record ProductionDashboard(IReadOnlyList<ProductionNeed> Products,int ProductsWithShortage,int UnitsToProduce,int ProducedToday,int TotalAvailable);
public sealed record ProductionNeed(Guid ProductId,string Product,int Required,int Physical,int Reserved,int Available,int Shortage,int PendingDemand,DateTimeOffset? NearestPromisedAtUtc);
public sealed record ProductionOrderDemand(Guid OrderId,long OrderNumber,string Customer,string Branch,DateTimeOffset? PromisedAtUtc,int Required,int Reserved,int Shortage,string Status,DateTimeOffset ConfirmedAtUtc);
public sealed record ReservationDemand(Guid ReservationId,Guid OrderId,int Shortage,DateTimeOffset? PromisedAtUtc,DateTimeOffset ConfirmedAtUtc,bool IsConfirmed=true);
public sealed record ReservationAssignment(Guid ReservationId,Guid OrderId,int Quantity);
public sealed record CreateSupplyCommand(string Name,string BaseUnit,string PurchasePresentation,decimal BaseQuantityPerPackage);
public sealed record UpdateSupplyCommand(Guid SupplyId,string Name,string BaseUnit,string PurchasePresentation,decimal BaseQuantityPerPackage);
public sealed record PurchaseCommand(Guid SupplyId,DateTimeOffset PurchasedAtUtc,string BaseUnit,string PurchasePresentation,decimal BaseQuantityPerPackage,decimal PackageQuantity,decimal UnitPrice,string PaymentMethod="",string? RenditionPerson=null,Stream? EvidenceContent=null,string? EvidenceFileName=null,string? EvidenceContentType=null,long EvidenceLength=0);
public sealed record SupplyCountCommand(Guid SupplyId,decimal CountedQuantity,string Reason);
public sealed record RecipeLineCommand(Guid SupplyId,decimal RequiredQuantity);
public sealed record SaveRecipeCommand(Guid ProductId,string Name,decimal YieldQuantity,IReadOnlyList<RecipeLineCommand> Lines);
public sealed record SupplyStockItem(Guid Id,string Name,string Unit,string PurchasePresentation,decimal BaseQuantityPerPackage,decimal Stock,decimal PackageEquivalent,decimal? LastUnitPrice,DateTimeOffset? LastPurchaseAtUtc,int Version);
public sealed record SupplyRequirement(Guid SupplyId,string Supply,string Unit,decimal Required,decimal Stock,decimal Shortage,string PurchasePresentation,decimal BaseQuantityPerPackage,decimal PackagesToBuy,decimal? EstimatedCost);
public sealed record SupplyPurchaseItem(Guid Id,string Supply,DateTimeOffset PurchasedAtUtc,decimal Packages,string Presentation,decimal BaseQuantity,string Unit,decimal UnitPrice,decimal TotalPrice,string RegisteredBy);
public sealed record SupplyMovementItem(Guid Id,string Supply,string Unit,string Kind,decimal Quantity,string Reason,DateTimeOffset AtUtc);
public sealed record ProductWithoutRecipe(Guid ProductId,string Product,int UnitsToProduce);
public sealed record SupplyInventoryDashboard(IReadOnlyList<SupplyStockItem> Supplies,IReadOnlyList<SupplyRequirement> Requirements,IReadOnlyList<SupplyPurchaseItem> RecentPurchases,IReadOnlyList<SupplyMovementItem> RecentMovements,IReadOnlyList<ProductWithoutRecipe> ProductsWithoutRecipe);
public sealed record RecipeLineDetail(Guid SupplyId,string Supply,string Unit,decimal RequiredQuantity);
public sealed record RecipeDetail(Guid Id,Guid ProductId,string Name,int Version,decimal YieldQuantity,IReadOnlyList<RecipeLineDetail> Lines);
public static class ProductionQuantities
{
 public static int GoodUnits(int produced,int waste)
 {
  if(produced<1)throw new ArgumentException("La cantidad producida debe ser mayor que cero.");
  if(waste<0)throw new ArgumentException("La merma no puede ser negativa.");
  if(waste>produced)throw new ArgumentException("La merma no puede superar la cantidad producida.");
  if(produced-waste<1)throw new ArgumentException("La producción debe dejar al menos una unidad aprovechable.");
  return produced-waste;
 }
}
public static class RecipeQuantities
{
 public static decimal Required(decimal recipeQuantity,decimal recipeYield,decimal productionUnits)
 {
  if(recipeQuantity<0)throw new ArgumentException("La cantidad de receta no puede ser negativa.");
  if(recipeYield<=0)throw new ArgumentException("El rendimiento debe ser mayor que cero.");
  if(productionUnits<0)throw new ArgumentException("La producción no puede ser negativa.");
  return decimal.Round(recipeQuantity/recipeYield*productionUnits,4);
 }
 public static decimal PackagesToBuy(decimal shortage,decimal contentPerPackage)
 {
  if(contentPerPackage<=0)throw new ArgumentException("El contenido del paquete debe ser mayor que cero.");
  return shortage<=0?0:Math.Ceiling(shortage/contentPerPackage);
 }
 public static decimal DropsToMilliliters(decimal drops)
 {
  if(drops<0)throw new ArgumentException("La cantidad de gotas no puede ser negativa.");
  return decimal.Round(drops*5m/100m,4);
 }
}
public static class ProductionAllocationPlanner
{
 public static IReadOnlyList<ReservationAssignment> Plan(int available,IEnumerable<ReservationDemand> demand)
 {
  if(available<=0)return [];
  var result=new List<ReservationAssignment>();
  foreach(var item in demand.Where(x=>x.IsConfirmed&&x.Shortage>0).OrderBy(x=>x.PromisedAtUtc??DateTimeOffset.MaxValue).ThenBy(x=>x.ConfirmedAtUtc))
  {
   var quantity=Math.Min(available,item.Shortage);if(quantity==0)break;
   result.Add(new(item.ReservationId,item.OrderId,quantity));available-=quantity;
  }
  return result;
 }
}
