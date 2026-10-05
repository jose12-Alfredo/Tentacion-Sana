namespace TentacionSana.Domain.Inventory;

public enum InventoryMovementKind { Production, DeliveredSale, Tasting, Sample, InternalConsumption, Waste, Expiration, Damage, Replacement, Donation, PositiveAdjustment, NegativeAdjustment }

public sealed class ProductCostVersion
{
    private ProductCostVersion(){} public Guid Id{get;private set;} public Guid ProductId{get;private set;} public decimal EstimatedUnitCost{get;private set;} public DateTimeOffset EffectiveFromUtc{get;private set;} public DateTimeOffset? EffectiveToUtc{get;private set;}
    public static ProductCostVersion Create(Guid productId,decimal cost,DateTimeOffset from){if(cost<0)throw new ArgumentException("El costo no puede ser negativo.");return new(){Id=Guid.NewGuid(),ProductId=productId,EstimatedUnitCost=decimal.Round(cost,2),EffectiveFromUtc=from};}
}

public sealed class RecipeVersion
{
    private RecipeVersion(){} public Guid Id{get;private set;} public Guid ProductId{get;private set;} public int Version{get;private set;} public string Name{get;private set;}=""; public decimal YieldQuantity{get;private set;} public DateTimeOffset EffectiveFromUtc{get;private set;} public DateTimeOffset? EffectiveToUtc{get;private set;} public bool IsValidated{get;private set;}
    public static RecipeVersion Create(Guid productId,int version,string name,DateTimeOffset from,decimal yieldQuantity=1)
    {
        if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("El nombre de la receta es obligatorio.");
        if(yieldQuantity<=0)throw new ArgumentException("El rendimiento de la receta debe ser mayor que cero.");
        return new(){Id=Guid.NewGuid(),ProductId=productId,Version=version,Name=name.Trim(),YieldQuantity=yieldQuantity,EffectiveFromUtc=from,IsValidated=true};
    }
    public void Close(DateTimeOffset at){if(at<EffectiveFromUtc)throw new ArgumentException("La fecha de cierre no es válida.");EffectiveToUtc=at;}
}

public enum MeasurementUnit { Unit, Gram, Milliliter }
public enum SupplyMovementKind { Purchase, ProductionConsumption, PositiveAdjustment, NegativeAdjustment, ProductionCorrection }

public sealed class Supply
{
    private Supply(){}
    public Guid Id{get;private set;} public string Name{get;private set;}=""; public MeasurementUnit BaseUnit{get;private set;}
    public string PurchasePresentation{get;private set;}=""; public decimal BaseQuantityPerPackage{get;private set;} public bool IsActive{get;private set;}
    public DateTimeOffset CreatedAtUtc{get;private set;} public DateTimeOffset UpdatedAtUtc{get;private set;}
    public static Supply Create(string name,MeasurementUnit baseUnit,string presentation,decimal baseQuantityPerPackage,DateTimeOffset now)
    {
        if(string.IsNullOrWhiteSpace(name)||name.Trim().Length>160)throw new ArgumentException("El nombre del insumo es obligatorio y admite hasta 160 caracteres.");
        if(string.IsNullOrWhiteSpace(presentation)||presentation.Trim().Length>160)throw new ArgumentException("La presentación de compra es obligatoria.");
        if(baseQuantityPerPackage<=0)throw new ArgumentException("El contenido de la presentación debe ser mayor que cero.");
        return new(){Id=Guid.NewGuid(),Name=name.Trim(),BaseUnit=baseUnit,PurchasePresentation=presentation.Trim(),BaseQuantityPerPackage=baseQuantityPerPackage,IsActive=true,CreatedAtUtc=now,UpdatedAtUtc=now};
    }
    public void CorrectBaseUnit(MeasurementUnit baseUnit,DateTimeOffset now){BaseUnit=baseUnit;UpdatedAtUtc=now;}
    public void Update(string name,MeasurementUnit baseUnit,string presentation,decimal baseQuantityPerPackage,DateTimeOffset now)
    {
        if(string.IsNullOrWhiteSpace(name)||name.Trim().Length>160)throw new ArgumentException("El nombre del insumo es obligatorio y admite hasta 160 caracteres.");
        if(string.IsNullOrWhiteSpace(presentation)||presentation.Trim().Length>160)throw new ArgumentException("La presentación de compra es obligatoria.");
        if(baseQuantityPerPackage<=0)throw new ArgumentException("El contenido de la presentación debe ser mayor que cero.");
        Name=name.Trim();BaseUnit=baseUnit;PurchasePresentation=presentation.Trim();BaseQuantityPerPackage=baseQuantityPerPackage;UpdatedAtUtc=now;
    }
}

public sealed class SupplyBalance
{
    private SupplyBalance(){} public Guid SupplyId{get;private set;} public decimal Quantity{get;private set;} public int Version{get;private set;}
    public static SupplyBalance Create(Guid supplyId)=>new(){SupplyId=supplyId,Version=1};
    public void Add(decimal quantity){if(quantity<=0)throw new ArgumentException("La cantidad debe ser mayor que cero.");Quantity+=quantity;Version++;}
    public void Consume(decimal quantity){if(quantity<=0)throw new ArgumentException("El consumo debe ser mayor que cero.");Quantity-=quantity;Version++;}
    public decimal Adjust(decimal counted){if(counted<0)throw new ArgumentException("El conteo físico no puede ser negativo.");var difference=counted-Quantity;Quantity=counted;Version++;return difference;}
}

public sealed class SupplyPurchase
{
    private SupplyPurchase(){} public Guid Id{get;private set;} public Guid SupplyId{get;private set;} public DateTimeOffset PurchasedAtUtc{get;private set;}
    public string PurchasePresentation{get;private set;}=""; public decimal PackageQuantity{get;private set;} public decimal UnitPrice{get;private set;} public decimal BaseQuantityPerPackage{get;private set;}
    public decimal TotalBaseQuantity{get;private set;} public decimal TotalPrice{get;private set;} public Guid RegisteredByUserId{get;private set;}
    public static SupplyPurchase Create(Guid supplyId,DateTimeOffset purchasedAt,string presentation,decimal packages,decimal unitPrice,decimal basePerPackage,Guid userId)
    {
        if(string.IsNullOrWhiteSpace(presentation)||presentation.Trim().Length>160)throw new ArgumentException("La presentación comprada es obligatoria.");
        if(packages<=0)throw new ArgumentException("La cantidad comprada debe ser mayor que cero.");
        if(unitPrice<0)throw new ArgumentException("El precio unitario no puede ser negativo.");
        if(basePerPackage<=0)throw new ArgumentException("La presentación de compra no es válida.");
        return new(){Id=Guid.NewGuid(),SupplyId=supplyId,PurchasedAtUtc=purchasedAt,PurchasePresentation=presentation.Trim(),PackageQuantity=packages,UnitPrice=decimal.Round(unitPrice,2),BaseQuantityPerPackage=basePerPackage,TotalBaseQuantity=decimal.Round(packages*basePerPackage,4),TotalPrice=decimal.Round(packages*unitPrice,2),RegisteredByUserId=userId};
    }
}

public sealed class RecipeIngredient
{
    private RecipeIngredient(){} public Guid Id{get;private set;} public Guid RecipeVersionId{get;private set;} public Guid SupplyId{get;private set;} public decimal RequiredQuantity{get;private set;}
    public static RecipeIngredient Create(Guid recipeVersionId,Guid supplyId,decimal requiredQuantity)
    {
        if(requiredQuantity<=0)throw new ArgumentException("La cantidad de la receta debe ser mayor que cero.");
        return new(){Id=Guid.NewGuid(),RecipeVersionId=recipeVersionId,SupplyId=supplyId,RequiredQuantity=requiredQuantity};
    }
}

public sealed class SupplyMovement
{
    private SupplyMovement(){} public Guid Id{get;private set;} public Guid SupplyId{get;private set;} public Guid? ProductionBatchId{get;private set;}
    public Guid? PurchaseId{get;private set;} public SupplyMovementKind Kind{get;private set;} public decimal Quantity{get;private set;}
    public decimal? HistoricalBaseUnitCost{get;private set;} public string Reason{get;private set;}=""; public Guid UserId{get;private set;} public DateTimeOffset OccurredAtUtc{get;private set;}
    public static SupplyMovement Create(Guid supplyId,Guid? batchId,Guid? purchaseId,SupplyMovementKind kind,decimal signedQuantity,decimal? baseUnitCost,string reason,Guid userId,DateTimeOffset now)
    {
        if(signedQuantity==0)throw new ArgumentException("El movimiento no puede ser cero.");
        if(string.IsNullOrWhiteSpace(reason))throw new ArgumentException("El motivo es obligatorio.");
        return new(){Id=Guid.NewGuid(),SupplyId=supplyId,ProductionBatchId=batchId,PurchaseId=purchaseId,Kind=kind,Quantity=decimal.Round(signedQuantity,4),HistoricalBaseUnitCost=baseUnitCost,Reason=reason.Trim(),UserId=userId,OccurredAtUtc=now};
    }
}

public sealed class SupplyStockCount
{
    private SupplyStockCount(){} public Guid Id{get;private set;} public Guid SupplyId{get;private set;} public decimal ExpectedQuantity{get;private set;}
    public decimal CountedQuantity{get;private set;} public decimal Difference{get;private set;} public string Reason{get;private set;}="";
    public Guid CountedByUserId{get;private set;} public DateTimeOffset CountedAtUtc{get;private set;}
    public static SupplyStockCount Create(Guid supplyId,decimal expected,decimal counted,string reason,Guid userId,DateTimeOffset now)
    {
        if(counted<0)throw new ArgumentException("El conteo físico no puede ser negativo.");
        if(string.IsNullOrWhiteSpace(reason))throw new ArgumentException("Indica el motivo del ajuste.");
        return new(){Id=Guid.NewGuid(),SupplyId=supplyId,ExpectedQuantity=expected,CountedQuantity=counted,Difference=counted-expected,Reason=reason.Trim(),CountedByUserId=userId,CountedAtUtc=now};
    }
}

public sealed class ProductStockBalance
{
    private ProductStockBalance(){} public Guid ProductId{get;private set;} public int PhysicalQuantity{get;private set;} public int ReservedQuantity{get;private set;} public int Version{get;private set;} public int AvailableQuantity=>PhysicalQuantity-ReservedQuantity;
    public static ProductStockBalance Create(Guid productId)=>new(){ProductId=productId,Version=1};
    public void Produce(int quantity){if(quantity<1)throw new ArgumentException("La producción debe ser positiva.");PhysicalQuantity+=quantity;Version++;}
    public int Reserve(int requested){if(requested<0)throw new ArgumentException("La reserva no puede ser negativa.");var assigned=Math.Min(Math.Max(0,AvailableQuantity),requested);ReservedQuantity+=assigned;Version++;return assigned;}
    public void Release(int quantity){if(quantity<0||quantity>ReservedQuantity)throw new InvalidOperationException("La liberación supera la reserva.");ReservedQuantity-=quantity;Version++;}
    public void Remove(int quantity){if(quantity<1||quantity>AvailableQuantity)throw new InvalidOperationException("Stock disponible insuficiente.");PhysicalQuantity-=quantity;Version++;}
    public void DeliverReserved(int quantity){if(quantity<1||quantity>ReservedQuantity||quantity>PhysicalQuantity)throw new InvalidOperationException("Reserva o stock físico insuficiente.");ReservedQuantity-=quantity;PhysicalQuantity-=quantity;Version++;}
    public void Adjust(int difference){if(PhysicalQuantity+difference<ReservedQuantity)throw new InvalidOperationException("El ajuste dejaría stock físico por debajo del reservado.");PhysicalQuantity+=difference;Version++;}
}

public sealed class ProductionBatch
{
    private ProductionBatch(){} public Guid Id{get;private set;} public string Number{get;private set;}="";public Guid ProductId{get;private set;}public Guid? RecipeVersionId{get;private set;}public DateTimeOffset ProducedAtUtc{get;private set;}public int GoodUnits{get;private set;}public int WasteUnits{get;private set;}public int RemainingUnits{get;private set;}public decimal? EstimatedUnitCost{get;private set;}public decimal? EstimatedTotalCost{get;private set;}public Guid CreatedByUserId{get;private set;}public int Version{get;private set;} public bool IsVoided{get;private set;} public DateTimeOffset? VoidedAtUtc{get;private set;} public Guid? VoidedByUserId{get;private set;} public string? VoidReason{get;private set;}
    public static ProductionBatch Create(string number,Guid product,Guid? recipe,DateTimeOffset produced,int good,int waste,decimal? unitCost,Guid user){if(good<1||waste<0)throw new ArgumentException("Las unidades del lote no son válidas.");return new(){Id=Guid.NewGuid(),Number=number,ProductId=product,RecipeVersionId=recipe,ProducedAtUtc=produced,GoodUnits=good,WasteUnits=waste,RemainingUnits=good,EstimatedUnitCost=unitCost is null?null:decimal.Round(unitCost.Value,2),EstimatedTotalCost=unitCost is null?null:decimal.Round(unitCost.Value*good,2),CreatedByUserId=user,Version=1};}
    public void Consume(int quantity){if(quantity<1||quantity>RemainingUnits)throw new InvalidOperationException("El lote no tiene unidades suficientes.");RemainingUnits-=quantity;Version++;}
    public int Correct(int good,int waste){if(IsVoided)throw new InvalidOperationException("El lote está anulado.");if(good<1||waste<0)throw new ArgumentException("Las cantidades corregidas no son válidas.");var consumed=GoodUnits-RemainingUnits;if(good<consumed)throw new InvalidOperationException($"No se puede reducir por debajo de las {consumed} unidades ya consumidas por FIFO.");var delta=good-GoodUnits;GoodUnits=good;WasteUnits=waste;RemainingUnits+=delta;EstimatedTotalCost=EstimatedUnitCost is null?null:decimal.Round(EstimatedUnitCost.Value*good,2);Version++;return delta;}
    public int Void(Guid user,DateTimeOffset now,string reason){if(IsVoided)throw new InvalidOperationException("El lote ya está anulado.");if(string.IsNullOrWhiteSpace(reason))throw new ArgumentException("El motivo es obligatorio.");var removable=RemainingUnits;RemainingUnits=0;IsVoided=true;VoidedAtUtc=now;VoidedByUserId=user;VoidReason=reason.Trim();Version++;return removable;}
}

public sealed class InventoryMovement
{
    private InventoryMovement(){} public Guid Id{get;private set;}public Guid ProductId{get;private set;}public Guid? ProductionBatchId{get;private set;}public InventoryMovementKind Kind{get;private set;}public int Quantity{get;private set;}public decimal? HistoricalUnitCost{get;private set;}public decimal? HistoricalTotalCost{get;private set;}public string Reason{get;private set;}="";public Guid UserId{get;private set;}public DateTimeOffset OccurredAtUtc{get;private set;}
    public static InventoryMovement Create(Guid product,Guid? batch,InventoryMovementKind kind,int signedQuantity,decimal? cost,string reason,Guid user,DateTimeOffset now){if(signedQuantity==0)throw new ArgumentException("El movimiento no puede ser cero.");if(string.IsNullOrWhiteSpace(reason))throw new ArgumentException("El motivo es obligatorio.");return new(){Id=Guid.NewGuid(),ProductId=product,ProductionBatchId=batch,Kind=kind,Quantity=signedQuantity,HistoricalUnitCost=cost is null?null:decimal.Round(cost.Value,2),HistoricalTotalCost=cost is null?null:decimal.Round(Math.Abs(signedQuantity)*cost.Value,2),Reason=reason.Trim(),UserId=user,OccurredAtUtc=now};}
}

public sealed class InventoryMovementAllocation { private InventoryMovementAllocation(){} public Guid Id{get;private set;}public Guid MovementId{get;private set;}public Guid ProductionBatchId{get;private set;}public int Quantity{get;private set;}public decimal? HistoricalUnitCost{get;private set;} public static InventoryMovementAllocation Create(Guid movement,Guid batch,int quantity,decimal? cost)=>new(){Id=Guid.NewGuid(),MovementId=movement,ProductionBatchId=batch,Quantity=quantity,HistoricalUnitCost=cost}; }
public sealed class StockCount { private StockCount(){} public Guid Id{get;private set;}public Guid ProductId{get;private set;}public int ExpectedQuantity{get;private set;}public int CountedQuantity{get;private set;}public int Difference{get;private set;}public string Reason{get;private set;}="";public Guid CountedByUserId{get;private set;}public DateTimeOffset CountedAtUtc{get;private set;} public static StockCount Create(Guid product,int expected,int counted,string reason,Guid user,DateTimeOffset now){if(counted<0||string.IsNullOrWhiteSpace(reason))throw new ArgumentException("El conteo y motivo son obligatorios.");return new(){Id=Guid.NewGuid(),ProductId=product,ExpectedQuantity=expected,CountedQuantity=counted,Difference=counted-expected,Reason=reason.Trim(),CountedByUserId=user,CountedAtUtc=now};}}
