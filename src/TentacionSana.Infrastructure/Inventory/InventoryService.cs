using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using TentacionSana.Application.Inventory;
using TentacionSana.Application.Security;
using TentacionSana.Domain.Inventory;
using TentacionSana.Domain.Orders;
using TentacionSana.Infrastructure.Auditing;
using TentacionSana.Infrastructure.Persistence;
using TentacionSana.Infrastructure.Identity;
using TentacionSana.Infrastructure.Media;
using TentacionSana.Domain.Finance;

namespace TentacionSana.Infrastructure.Inventory;

#pragma warning disable CA1725
public sealed class InventoryService(ApplicationDbContext db, TimeProvider clock, UserManager<ApplicationUser> users, IOptions<CloudinaryOptions> cloudinaryOptions) : IInventoryService
{
    private static readonly OrderStatus[] DemandStatuses =
        [OrderStatus.Confirmed, OrderStatus.InPreparation, OrderStatus.Ready, OrderStatus.OutForDelivery];

    public async Task<InventoryResult> RegisterProductionAsync(ProductionCommand command, Guid userId, CancellationToken ct = default)
    {
        int goodUnits;
        try { goodUnits = ProductionQuantities.GoodUnits(command.ProducedUnits, command.WasteUnits); }
        catch (ArgumentException ex) { return Fail(ex.Message); }
        if (!await db.Products.AnyAsync(x => x.Id == command.ProductId && x.IsActive, ct)) return Fail("El producto no existe o está inactivo.");

        var executionStrategy = db.Database.CreateExecutionStrategy();
        try
        {
            return await executionStrategy.ExecuteAsync(async () =>
            {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var balance = await Balance(command.ProductId, ct);
            var now = clock.GetUtcNow();
            var number = $"L-{command.ProducedAtUtc:yyyyMMdd}-{Guid.NewGuid():N}"[..20];
            var recipe = await db.RecipeVersions
                .Where(x => x.ProductId == command.ProductId && x.EffectiveFromUtc <= command.ProducedAtUtc && (x.EffectiveToUtc == null || x.EffectiveToUtc > command.ProducedAtUtc))
                .OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
            var ingredients = recipe is null ? [] : await db.RecipeIngredients.Where(x => x.RecipeVersionId == recipe.Id).ToListAsync(ct);
            var latestCosts = new Dictionary<Guid, decimal>();
            foreach (var supplyId in ingredients.Select(x => x.SupplyId).Distinct())
            {
                var purchase = await db.SupplyPurchases.AsNoTracking().Where(x => x.SupplyId == supplyId).OrderByDescending(x => x.PurchasedAtUtc).FirstOrDefaultAsync(ct);
                if (purchase is not null) latestCosts[supplyId] = purchase.UnitPrice / purchase.BaseQuantityPerPackage;
            }
            decimal? estimatedUnitCost = ingredients.Count > 0 && ingredients.All(x => latestCosts.ContainsKey(x.SupplyId))
                ? decimal.Round(ingredients.Sum(x => RecipeQuantities.Required(x.RequiredQuantity, recipe!.YieldQuantity, command.ProducedUnits) * latestCosts[x.SupplyId]) / goodUnits, 2)
                : null;
            var batch = ProductionBatch.Create(number, command.ProductId, recipe?.Id, command.ProducedAtUtc, goodUnits, command.WasteUnits, estimatedUnitCost, userId);
            var movement = InventoryMovement.Create(command.ProductId, batch.Id, InventoryMovementKind.Production, goodUnits, null,
                $"Producción: {command.ProducedUnits}; merma: {command.WasteUnits}; aprovechable: {goodUnits}", userId, now);
            balance.Produce(goodUnits);
            db.ProductionBatches.Add(batch);
            db.InventoryMovements.Add(movement);

            var warnings = new List<string>();
            if (recipe is null || ingredients.Count == 0) warnings.Add("El producto no tiene una receta configurada; no se descontaron insumos.");
            foreach (var ingredient in ingredients)
            {
                var required = RecipeQuantities.Required(ingredient.RequiredQuantity, recipe!.YieldQuantity, command.ProducedUnits);
                var supplyBalance = await SupplyBalance(ingredient.SupplyId, ct);
                supplyBalance.Consume(required);
                db.SupplyMovements.Add(SupplyMovement.Create(ingredient.SupplyId, batch.Id, null, SupplyMovementKind.ProductionConsumption, -required,
                    latestCosts.GetValueOrDefault(ingredient.SupplyId), $"Consumo calculado para el lote {number}", userId, now));
                if (supplyBalance.Quantity < 0)
                {
                    var supply = await db.Supplies.AsNoTracking().SingleAsync(x => x.Id == ingredient.SupplyId, ct);
                    warnings.Add($"{supply.Name} quedó con saldo negativo de {Math.Abs(supplyBalance.Quantity):0.####} {UnitText(supply.BaseUnit)}.");
                }
            }

            var reservations = await db.StockReservations.Where(x => x.ProductId == command.ProductId && x.IsActive && x.ShortageQuantity > 0).ToListAsync(ct);
            var orderIds = reservations.Select(x => x.OrderId).Distinct().ToList();
            var priorities = await db.Orders.AsNoTracking().Where(x => orderIds.Contains(x.Id) && DemandStatuses.Contains(x.Status))
                .Select(x => new
                {
                    x.Id, x.PromisedAtUtc,
                    ConfirmedAtUtc = x.StatusHistory.Where(h => h.NewStatus == OrderStatus.Confirmed).OrderBy(h => h.ChangedAtUtc)
                        .Select(h => (DateTimeOffset?)h.ChangedAtUtc).FirstOrDefault() ?? x.CreatedAtUtc
                }).ToDictionaryAsync(x => x.Id, ct);
            var plan = ProductionAllocationPlanner.Plan(balance.AvailableQuantity,
                reservations.Where(x => priorities.ContainsKey(x.OrderId)).Select(x => new ReservationDemand(
                    x.Id, x.OrderId, x.ShortageQuantity, priorities[x.OrderId].PromisedAtUtc, priorities[x.OrderId].ConfirmedAtUtc)));
            var assignedUnits = 0;
            var assignedOrders = new HashSet<Guid>();
            foreach (var assignment in plan)
            {
                var reservation = reservations.Single(x => x.Id == assignment.ReservationId);
                var assigned = balance.Reserve(assignment.Quantity);
                reservation.Allocate(reservation.ReservedQuantity + assigned);
                assignedUnits += assigned;
                assignedOrders.Add(assignment.OrderId);
            }

            db.AuditEntries.Add(new AuditEntry
            {
                Id = Guid.NewGuid(), Action = "ProductionRegistered", EntityType = "Inventory", EntityId = batch.Id.ToString(),
                UserId = userId, OccurredAtUtc = now,
                NewValuesJson = JsonSerializer.Serialize(new { command.ProductId, command.ProducedUnits, command.WasteUnits, GoodUnits = goodUnits, BatchNumber = number, AssignedUnits = assignedUnits, AssignedOrders = assignedOrders.Count }),
                Reason = $"Producción registrada: {command.ProducedUnits}. Merma: {command.WasteUnits}. Aprovechable: {goodUnits}. {assignedUnits} unidades reasignadas a {assignedOrders.Count} pedidos."
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new InventoryResult(true, batch.Id, [], assignedUnits, assignedOrders.Count, warnings);
            });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            return Fail(ex is DbUpdateConcurrencyException ? "El inventario cambió durante el registro. Vuelve a intentarlo." : ex.Message);
        }
        catch (DbUpdateException) { return Fail("La producción no pudo guardarse. Intenta nuevamente."); }
    }

    public async Task<ProductionDashboard> GetProductionDashboardAsync(CancellationToken ct = default)
    {
        var products = await db.Products.AsNoTracking().Where(x => x.IsActive).Select(x => new { x.Id, x.Name }).ToListAsync(ct);
        var balances = await db.ProductStockBalances.AsNoTracking().ToDictionaryAsync(x => x.ProductId, ct);
        var demand = await (from reservation in db.StockReservations.AsNoTracking()
                            join order in db.Orders.AsNoTracking() on reservation.OrderId equals order.Id
                            where reservation.IsActive && DemandStatuses.Contains(order.Status)
                            select new { reservation.ProductId, reservation.Quantity, reservation.ReservedQuantity, reservation.ShortageQuantity, order.PromisedAtUtc }).ToListAsync(ct);
        var pending = await (from line in db.OrderLines.AsNoTracking()
                             join order in db.Orders.AsNoTracking() on line.OrderId equals order.Id
                             where line.IsActive && order.Status == OrderStatus.Draft
                             select new { line.ProductId, line.Quantity }).ToListAsync(ct);
        var needs = products.Select(product =>
        {
            var productDemand = demand.Where(x => x.ProductId == product.Id).ToList();
            balances.TryGetValue(product.Id, out var balance);
            return new ProductionNeed(product.Id, product.Name, productDemand.Sum(x => x.Quantity), balance?.PhysicalQuantity ?? 0,
                balance?.ReservedQuantity ?? 0, balance?.AvailableQuantity ?? 0, productDemand.Sum(x => x.ShortageQuantity),
                pending.Where(x => x.ProductId == product.Id).Sum(x => x.Quantity),
                productDemand.Where(x => x.ShortageQuantity > 0).Select(x => x.PromisedAtUtc).OrderBy(x => x).FirstOrDefault());
        }).Where(x => x.Required > 0 || x.PendingDemand > 0)
          .OrderBy(x => x.NearestPromisedAtUtc ?? DateTimeOffset.MaxValue).ThenByDescending(x => x.Shortage).ThenBy(x => x.Product).ToList();
        var localToday = clock.GetLocalNow().Date;
        var start = new DateTimeOffset(localToday, clock.GetLocalNow().Offset).ToUniversalTime();
        var end = start.AddDays(1);
        var producedToday = await db.ProductionBatches.AsNoTracking().Where(x => !x.IsVoided && x.ProducedAtUtc >= start && x.ProducedAtUtc < end).SumAsync(x => x.GoodUnits, ct);
        return new ProductionDashboard(needs, needs.Count(x => x.Shortage > 0), needs.Sum(x => x.Shortage), producedToday, balances.Values.Sum(x => Math.Max(0, x.AvailableQuantity)));
    }

    public async Task<InventoryResult> UpdateProductionBatchAsync(UpdateProductionBatchCommand command, Guid userId, CancellationToken ct = default)
    {
        if (!await IsAdministrator(userId)) return Fail("Solo una persona administradora puede corregir lotes.");
        int goodUnits;
        try { goodUnits = ProductionQuantities.GoodUnits(command.ProducedUnits, command.WasteUnits); }
        catch (ArgumentException ex) { return Fail(ex.Message); }
        if (string.IsNullOrWhiteSpace(command.Reason)) return Fail("Indica el motivo de la corrección.");
        var executionStrategy = db.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var batch = await db.ProductionBatches.SingleOrDefaultAsync(x => x.Id == command.BatchId, ct);
            if (batch is null || batch.IsVoided) return Fail("El lote no existe o fue anulado.");
            if (batch.Version != command.Version) return Fail("El lote cambió. Recarga la pantalla antes de corregirlo.");
            var previousProduced = batch.GoodUnits + batch.WasteUnits;
            var previous = new { ProducedUnits = previousProduced, batch.GoodUnits, batch.WasteUnits, batch.RemainingUnits };
            var balance = await Balance(batch.ProductId, ct);
            var delta = goodUnits - batch.GoodUnits;
            if (delta < 0) await FreeReservedStockAsync(batch.ProductId, -delta, balance, ct);
            batch.Correct(goodUnits, command.WasteUnits);
            var now = clock.GetUtcNow();
            if (delta > 0) { balance.Produce(delta); await AssignShortagesAsync(batch.ProductId, balance, ct); }
            else if (delta < 0) balance.Remove(-delta);
            if (delta != 0) db.InventoryMovements.Add(InventoryMovement.Create(batch.ProductId, batch.Id,
                delta > 0 ? InventoryMovementKind.PositiveAdjustment : InventoryMovementKind.NegativeAdjustment,
                delta, null, $"Corrección administrativa del lote {batch.Number}: {command.Reason}", userId, now));
            await AdjustRecipeConsumptionAsync(batch, command.ProducedUnits - previousProduced, command.Reason, userId, now, ct);
            db.AuditEntries.Add(new AuditEntry { Id = Guid.NewGuid(), UserId = userId, Action = "ProductionBatchCorrected", EntityType = "ProductionBatch", EntityId = batch.Id.ToString(), PreviousValuesJson = JsonSerializer.Serialize(previous), NewValuesJson = JsonSerializer.Serialize(new { command.ProducedUnits, GoodUnits = goodUnits, command.WasteUnits, batch.RemainingUnits }), Reason = command.Reason.Trim(), OccurredAtUtc = now });
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return Ok(batch.Id);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException) { await transaction.RollbackAsync(ct); db.ChangeTracker.Clear(); return Fail(ex.Message); }
        });
    }

    public async Task<InventoryResult> DeleteProductionBatchAsync(DeleteProductionBatchCommand command, Guid userId, CancellationToken ct = default)
    {
        if (!await IsAdministrator(userId)) return Fail("Solo una persona administradora puede eliminar lotes.");
        if (string.IsNullOrWhiteSpace(command.Reason)) return Fail("Indica el motivo de eliminación.");
        var executionStrategy = db.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var batch = await db.ProductionBatches.SingleOrDefaultAsync(x => x.Id == command.BatchId, ct);
            if (batch is null || batch.IsVoided) return Fail("El lote no existe o ya fue eliminado.");
            if (batch.Version != command.Version) return Fail("El lote cambió. Recarga la pantalla antes de eliminarlo.");
            var previous = new { batch.Number, batch.ProductId, ProducedUnits = batch.GoodUnits + batch.WasteUnits, batch.GoodUnits, batch.WasteUnits, batch.RemainingUnits };
            var balance = await Balance(batch.ProductId, ct);
            var removable = batch.RemainingUnits;
            if (removable > 0) { await FreeReservedStockAsync(batch.ProductId, removable, balance, ct); balance.Remove(removable); }
            var now = clock.GetUtcNow(); batch.Void(userId, now, command.Reason);
            if (removable > 0) db.InventoryMovements.Add(InventoryMovement.Create(batch.ProductId, batch.Id, InventoryMovementKind.NegativeAdjustment, -removable, null, $"Lote eliminado por administración: {command.Reason}", userId, now));
            var consumption = await db.SupplyMovements.AsNoTracking()
                .Where(x => x.ProductionBatchId == batch.Id &&
                    (x.Kind == SupplyMovementKind.ProductionConsumption || x.Kind == SupplyMovementKind.ProductionCorrection))
                .GroupBy(x => x.SupplyId)
                .Select(group => new { SupplyId = group.Key, Quantity = group.Sum(x => x.Quantity) })
                .ToListAsync(ct);
            foreach (var item in consumption)
            {
                if (item.Quantity == 0) continue;
                var supplyBalance = await SupplyBalance(item.SupplyId, ct);
                if (item.Quantity < 0) supplyBalance.Add(-item.Quantity);
                else supplyBalance.Consume(item.Quantity);
                db.SupplyMovements.Add(SupplyMovement.Create(item.SupplyId, batch.Id, null,
                    SupplyMovementKind.ProductionCorrection, -item.Quantity, null,
                    $"Reversión del consumo del lote {batch.Number}: {command.Reason}", userId, now));
            }
            db.AuditEntries.Add(new AuditEntry { Id = Guid.NewGuid(), UserId = userId, Action = "ProductionBatchDeleted", EntityType = "ProductionBatch", EntityId = batch.Id.ToString(), PreviousValuesJson = JsonSerializer.Serialize(previous), NewValuesJson = JsonSerializer.Serialize(new { IsVoided = true, RemovedFromStock = removable }), Reason = command.Reason.Trim(), OccurredAtUtc = now });
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return Ok(batch.Id);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException) { await transaction.RollbackAsync(ct); db.ChangeTracker.Clear(); return Fail(ex.Message); }
        });
    }

    public async Task<IReadOnlyList<ProductionOrderDemand>> GetProductionOrderDemandAsync(Guid productId, CancellationToken ct = default)
    {
        var rows = await (from reservation in db.StockReservations.AsNoTracking()
                          join order in db.Orders.AsNoTracking() on reservation.OrderId equals order.Id
                          join customer in db.Customers.AsNoTracking() on order.CustomerId equals customer.Id
                          join point in db.DeliveryPoints.AsNoTracking() on order.DeliveryPointId equals (Guid?)point.Id into points
                          from point in points.DefaultIfEmpty()
                          where reservation.ProductId == productId && reservation.IsActive && DemandStatuses.Contains(order.Status)
                          select new ProductionOrderDemand(order.Id, order.Number, customer.Name, point == null ? "—" : point.Label, order.PromisedAtUtc,
                              reservation.Quantity, reservation.ReservedQuantity, reservation.ShortageQuantity, order.Status.ToString(),
                              order.StatusHistory.Where(h => h.NewStatus == OrderStatus.Confirmed).OrderBy(h => h.ChangedAtUtc)
                                  .Select(h => (DateTimeOffset?)h.ChangedAtUtc).FirstOrDefault() ?? order.CreatedAtUtc)).ToListAsync(ct);
        return rows.OrderBy(x => x.PromisedAtUtc ?? DateTimeOffset.MaxValue).ThenBy(x => x.ConfirmedAtUtc).ThenBy(x => x.OrderNumber).ToList();
    }

    public async Task<InventoryResult> RegisterOutputAsync(OutputCommand command, Guid userId, CancellationToken ct = default)
    {
        if (command.Quantity < 1) return Fail("La cantidad debe ser mayor que cero.");
        if (!Enum.TryParse<InventoryMovementKind>(command.Kind, true, out var kind) || kind is InventoryMovementKind.Production or InventoryMovementKind.PositiveAdjustment) return Fail("Tipo de salida inválido.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var balance = await Balance(command.ProductId, ct);
        if (balance.AvailableQuantity < command.Quantity) return Fail("Stock disponible insuficiente.");
        var batches = await db.ProductionBatches.Where(x => x.ProductId == command.ProductId && x.RemainingUnits > 0).OrderBy(x => x.ProducedAtUtc).ThenBy(x => x.Number).ToListAsync(ct);
        var remaining = command.Quantity; var allocations = new List<(ProductionBatch Batch, int Quantity)>();
        foreach (var batch in batches) { var take = Math.Min(remaining, batch.RemainingUnits); if (take > 0) { batch.Consume(take); allocations.Add((batch, take)); remaining -= take; } if (remaining == 0) break; }
        if (remaining > 0) return Fail("Los lotes no respaldan el saldo físico.");
        decimal? weightedCost = allocations.Any(x => x.Batch.EstimatedUnitCost is null) ? null : allocations.Sum(x => x.Quantity * x.Batch.EstimatedUnitCost!.Value) / command.Quantity;
        var now = clock.GetUtcNow(); var movement = InventoryMovement.Create(command.ProductId, null, kind, -command.Quantity, weightedCost, command.Reason, userId, now);
        db.InventoryMovements.Add(movement);
        foreach (var allocation in allocations) db.InventoryMovementAllocations.Add(InventoryMovementAllocation.Create(movement.Id, allocation.Batch.Id, allocation.Quantity, allocation.Batch.EstimatedUnitCost));
        balance.Remove(command.Quantity); Audit("InventoryOutput", movement.Id, userId, now); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return Ok(movement.Id);
    }

    public async Task<InventoryResult> RegisterCountAsync(CountCommand command, Guid userId, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct); var balance = await Balance(command.ProductId, ct);
        var count = StockCount.Create(command.ProductId, balance.PhysicalQuantity, command.CountedQuantity, command.Reason, userId, clock.GetUtcNow());
        if (count.Difference < 0 && command.CountedQuantity < balance.ReservedQuantity) return Fail("El conteo no puede dejar stock por debajo de lo reservado.");
        balance.Adjust(count.Difference); db.StockCounts.Add(count); db.InventoryMovements.Add(InventoryMovement.Create(command.ProductId, null, count.Difference >= 0 ? InventoryMovementKind.PositiveAdjustment : InventoryMovementKind.NegativeAdjustment, count.Difference, 0, command.Reason, userId, clock.GetUtcNow()));
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return Ok(count.Id);
    }

    public async Task<SupplyInventoryDashboard> GetSupplyDashboardAsync(CancellationToken ct = default)
    {
        var supplies = await db.Supplies.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(ct);
        var balances = await db.SupplyBalances.AsNoTracking().ToDictionaryAsync(x => x.SupplyId, ct);
        var allPurchases = await db.SupplyPurchases.AsNoTracking().OrderByDescending(x => x.PurchasedAtUtc).ToListAsync(ct);
        var lastPurchases = allPurchases.GroupBy(x => x.SupplyId).ToDictionary(x => x.Key, x => x.First());
        var stock = supplies.Select(supply =>
        {
            balances.TryGetValue(supply.Id, out var balance);
            lastPurchases.TryGetValue(supply.Id, out var purchase);
            var quantity = balance?.Quantity ?? 0;
            return new SupplyStockItem(supply.Id, supply.Name, UnitText(supply.BaseUnit), supply.PurchasePresentation,
                supply.BaseQuantityPerPackage, quantity, quantity / supply.BaseQuantityPerPackage, purchase?.UnitPrice,
                purchase?.PurchasedAtUtc, balance?.Version ?? 0);
        }).ToList();

        var production = await GetProductionDashboardAsync(ct);
        var activeRecipes = (await db.RecipeVersions.AsNoTracking().Where(x => x.EffectiveToUtc == null).ToListAsync(ct))
            .GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.OrderByDescending(r => r.Version).First());
        var recipeIds = activeRecipes.Values.Select(x => x.Id).ToList();
        var components = await db.RecipeIngredients.AsNoTracking().Where(x => recipeIds.Contains(x.RecipeVersionId)).ToListAsync(ct);
        var productsWithoutRecipe = new List<ProductWithoutRecipe>();
        var calculated = new List<(Guid SupplyId, decimal Required)>();
        foreach (var need in production.Products.Where(x => x.Shortage > 0))
        {
            if (!activeRecipes.TryGetValue(need.ProductId, out var recipe))
            {
                productsWithoutRecipe.Add(new(need.ProductId, need.Product, need.Shortage));
                continue;
            }
            var lines = components.Where(x => x.RecipeVersionId == recipe.Id).ToList();
            if (lines.Count == 0)
            {
                productsWithoutRecipe.Add(new(need.ProductId, need.Product, need.Shortage));
                continue;
            }
            calculated.AddRange(lines.Select(x => (x.SupplyId, RecipeQuantities.Required(x.RequiredQuantity, recipe.YieldQuantity, need.Shortage))));
        }
        var supplyMap = supplies.ToDictionary(x => x.Id);
        var requirements = calculated.GroupBy(x => x.SupplyId).Where(x => supplyMap.ContainsKey(x.Key)).Select(group =>
        {
            var supply = supplyMap[group.Key];
            var required = group.Sum(x => x.Required);
            var current = balances.GetValueOrDefault(group.Key)?.Quantity ?? 0;
            var shortage = Math.Max(0, required - current);
            var packages = RecipeQuantities.PackagesToBuy(shortage, supply.BaseQuantityPerPackage);
            var price = lastPurchases.GetValueOrDefault(group.Key)?.UnitPrice;
            return new SupplyRequirement(supply.Id, supply.Name, UnitText(supply.BaseUnit), required, current, shortage,
                supply.PurchasePresentation, supply.BaseQuantityPerPackage, packages, price is null ? null : packages * price.Value);
        }).OrderByDescending(x => x.Shortage > 0).ThenByDescending(x => x.Shortage).ThenBy(x => x.Supply).ToList();

        var userNames = await db.Users.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
        var recentPurchases = allPurchases.Take(20).Where(x => supplyMap.ContainsKey(x.SupplyId)).Select(x =>
        {
            var supply = supplyMap[x.SupplyId];
            return new SupplyPurchaseItem(x.Id, supply.Name, x.PurchasedAtUtc, x.PackageQuantity, x.PurchasePresentation,
                x.TotalBaseQuantity, UnitText(supply.BaseUnit), x.UnitPrice, x.TotalPrice, userNames.GetValueOrDefault(x.RegisteredByUserId) ?? "—");
        }).ToList();
        var movementRows = await db.SupplyMovements.AsNoTracking()
            .Where(x => x.ProductionBatchId == null ||
                !db.ProductionBatches.Any(batch => batch.Id == x.ProductionBatchId && batch.IsVoided))
            .OrderByDescending(x => x.OccurredAtUtc).Take(30).ToListAsync(ct);
        var movements = movementRows.Where(x => supplyMap.ContainsKey(x.SupplyId)).Select(x =>
        {
            var supply = supplyMap[x.SupplyId];
            return new SupplyMovementItem(x.Id, supply.Name, UnitText(supply.BaseUnit), MovementText(x.Kind), x.Quantity, x.Reason, x.OccurredAtUtc);
        }).ToList();
        return new(stock, requirements, recentPurchases, movements, productsWithoutRecipe);
    }

    public async Task<RecipeDetail?> GetRecipeAsync(Guid productId, CancellationToken ct = default)
    {
        var recipe = await db.RecipeVersions.AsNoTracking().Where(x => x.ProductId == productId && x.EffectiveToUtc == null)
            .OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        if (recipe is null) return null;
        var lines = await (from ingredient in db.RecipeIngredients.AsNoTracking()
                           join supply in db.Supplies.AsNoTracking() on ingredient.SupplyId equals supply.Id
                           where ingredient.RecipeVersionId == recipe.Id
                           orderby supply.Name
                           select new RecipeLineDetail(supply.Id, supply.Name, UnitText(supply.BaseUnit), ingredient.RequiredQuantity)).ToListAsync(ct);
        return new(recipe.Id, recipe.ProductId, recipe.Name, recipe.Version, recipe.YieldQuantity, lines);
    }

    public async Task<InventoryResult> CreateSupplyAsync(CreateSupplyCommand command, Guid userId, CancellationToken ct = default)
    {
        if (!Enum.TryParse<MeasurementUnit>(command.BaseUnit, true, out var unit)) return Fail("Selecciona una unidad base válida.");
        var suppliedName = command.Name.Trim();
        var escapedName = suppliedName.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
        if (await db.Supplies.AnyAsync(x => EF.Functions.ILike(x.Name, escapedName, "\\"), ct)) return Fail("Ya existe un insumo con ese nombre.");
        try
        {
            var now = clock.GetUtcNow();
            var supply = Supply.Create(command.Name, unit, command.PurchasePresentation, command.BaseQuantityPerPackage, now);
            db.Supplies.Add(supply); db.SupplyBalances.Add(TentacionSana.Domain.Inventory.SupplyBalance.Create(supply.Id));
            db.AuditEntries.Add(new AuditEntry { Id = Guid.NewGuid(), Action = "SupplyCreated", EntityType = "Supply", EntityId = supply.Id.ToString(), UserId = userId, OccurredAtUtc = now, NewValuesJson = JsonSerializer.Serialize(command) });
            await db.SaveChangesAsync(ct); return Ok(supply.Id);
        }
        catch (ArgumentException ex) { return Fail(ex.Message); }
    }

    public async Task<InventoryResult> UpdateSupplyAsync(UpdateSupplyCommand command, Guid userId, CancellationToken ct = default)
    {
        if (!Enum.TryParse<MeasurementUnit>(command.BaseUnit, true, out var unit)) return Fail("Selecciona una unidad base válida.");
        try
        {
            var supply = await db.Supplies.SingleOrDefaultAsync(x => x.Id == command.SupplyId && x.IsActive, ct);
            if (supply is null) return Fail("El insumo no existe o está inactivo.");
            if (await db.Supplies.AnyAsync(x => x.Id != supply.Id && EF.Functions.ILike(x.Name, command.Name.Trim()), ct)) return Fail("Ya existe otro insumo con ese nombre.");
            if (supply.BaseUnit != unit)
            {
                var hasUsage = await db.SupplyMovements.Where(x => x.SupplyId == supply.Id).Select(x => x.SupplyId)
                    .Concat(db.RecipeIngredients.Where(x => x.SupplyId == supply.Id).Select(x => x.SupplyId)).AnyAsync(ct);
                var balance = await db.SupplyBalances.AsNoTracking().Where(x => x.SupplyId == supply.Id).Select(x => (decimal?)x.Quantity).SingleOrDefaultAsync(ct) ?? 0;
                if (balance != 0 || hasUsage) return Fail($"No se puede cambiar de {UnitText(supply.BaseUnit)} a {UnitText(unit)} porque el insumo ya tiene saldo, movimientos o recetas.");
            }
            var previous = new { supply.Name, supply.BaseUnit, supply.PurchasePresentation, supply.BaseQuantityPerPackage };
            var now = clock.GetUtcNow();
            supply.Update(command.Name, unit, command.PurchasePresentation, command.BaseQuantityPerPackage, now);
            db.AuditEntries.Add(new AuditEntry { Id = Guid.NewGuid(), Action = "SupplyUpdated", EntityType = "Supply", EntityId = supply.Id.ToString(), UserId = userId, OccurredAtUtc = now, PreviousValuesJson = JsonSerializer.Serialize(previous), NewValuesJson = JsonSerializer.Serialize(command) });
            await db.SaveChangesAsync(ct); return Ok(supply.Id);
        }
        catch (ArgumentException ex) { return Fail(ex.Message); }
        catch (DbUpdateException) { return Fail("No se pudo actualizar el insumo. Verifica los datos e intenta nuevamente."); }
    }

    public async Task<InventoryResult> RegisterPurchaseAsync(PurchaseCommand command, Guid userId, CancellationToken ct = default)
    {
        if (!Enum.TryParse<MeasurementUnit>(command.BaseUnit, true, out var purchaseUnit)) return Fail("Selecciona una unidad de medida válida.");
        if (command.PaymentMethod is not ("Qr" or "Cash" or "Rendition")) return Fail("Selecciona QR, efectivo o rendición como forma de pago.");
        if (command.EvidenceContent is null || string.IsNullOrWhiteSpace(command.EvidenceFileName) || command.EvidenceLength<=0) return Fail("La foto del comprobante de compra es obligatoria.");
        if (command.PaymentMethod=="Rendition"&&string.IsNullOrWhiteSpace(command.RenditionPerson)) return Fail("Indica la persona a quien se debe la rendición.");
        var allowedTypes=new[]{"image/jpeg","image/png","image/webp"};
        if(string.IsNullOrWhiteSpace(command.EvidenceContentType)||!allowedTypes.Contains(command.EvidenceContentType.ToLowerInvariant())||command.EvidenceLength>10*1024*1024)return Fail("El comprobante debe ser JPG, PNG o WebP de hasta 10 MB.");
        var settings=cloudinaryOptions.Value;var cloudinary=new Cloudinary(new Account(settings.CloudName,settings.ApiKey,settings.ApiSecret)){Api={Secure=true}};
        var upload=await cloudinary.UploadAsync(new ImageUploadParams{File=new FileDescription(Path.GetFileName(command.EvidenceFileName),command.EvidenceContent),Folder="tentacion-sana/arqueo/compras",Type="authenticated",UniqueFilename=true,Overwrite=false,UseFilename=true},ct);
        if(upload.Error is not null||string.IsNullOrWhiteSpace(upload.PublicId))return Fail(upload.Error?.Message??"No se pudo guardar el comprobante de compra.");
        try
        {
            var state = await (from inventorySupply in db.Supplies
                               join existingBalance in db.SupplyBalances on inventorySupply.Id equals existingBalance.SupplyId into balances
                               from existingBalance in balances.DefaultIfEmpty()
                               where inventorySupply.Id == command.SupplyId && inventorySupply.IsActive
                               select new { Supply = inventorySupply, Balance = existingBalance }).SingleOrDefaultAsync(ct);
            if (state is null) return Fail("El insumo no existe o está inactivo.");
            var supply = state.Supply;
            var balance = state.Balance;
            if (balance is null)
            {
                balance = TentacionSana.Domain.Inventory.SupplyBalance.Create(supply.Id);
                db.SupplyBalances.Add(balance);
            }
            var now = clock.GetUtcNow();
            if (supply.BaseUnit != purchaseUnit)
            {
                var hasUsage = await db.SupplyMovements.Where(x => x.SupplyId == supply.Id).Select(x => x.SupplyId)
                    .Concat(db.RecipeIngredients.Where(x => x.SupplyId == supply.Id).Select(x => x.SupplyId)).AnyAsync(ct);
                var hasHistory = balance.Quantity != 0 || hasUsage;
                if (hasHistory) return Fail($"{supply.Name} ya tiene saldo, movimientos o recetas en {UnitText(supply.BaseUnit)}. Corrige el inventario antes de cambiarlo a {UnitText(purchaseUnit)}.");
                supply.CorrectBaseUnit(purchaseUnit, now);
            }
            var purchase = TentacionSana.Domain.Inventory.SupplyPurchase.Create(supply.Id, command.PurchasedAtUtc, command.PurchasePresentation, command.PackageQuantity, command.UnitPrice, command.BaseQuantityPerPackage, userId);
            balance.Add(purchase.TotalBaseQuantity);
            db.SupplyPurchases.Add(purchase);
            db.SupplyMovements.Add(SupplyMovement.Create(supply.Id, null, purchase.Id, SupplyMovementKind.Purchase, purchase.TotalBaseQuantity,
                purchase.UnitPrice / purchase.BaseQuantityPerPackage, $"Compra de {purchase.PackageQuantity:0.####} {purchase.PurchasePresentation}", userId, now));
            if(command.PaymentMethod=="Rendition") db.CashPayables.Add(CashPayable.Create(purchase.Id,command.RenditionPerson!,purchase.TotalPrice,upload.PublicId,command.EvidenceFileName,upload.Format??"",upload.Bytes,userId,now));
            else db.CashMovements.Add(CashMovement.Create(command.PaymentMethod=="Qr"?CashAccount.Bank:CashAccount.Cash,CashDirection.Expense,CashSource.InventoryPurchase,command.PurchasedAtUtc,$"Compra de {supply.Name}: {purchase.PackageQuantity:0.####} {purchase.PurchasePresentation}",purchase.TotalPrice,upload.PublicId,command.EvidenceFileName,upload.Format??"",upload.Bytes,userId,now,supplyPurchaseId:purchase.Id,category:"Ingredientes"));
            db.AuditEntries.Add(new AuditEntry { Id = Guid.NewGuid(), Action = "SupplyPurchaseRegistered", EntityType = "SupplyPurchase", EntityId = purchase.Id.ToString(), UserId = userId, OccurredAtUtc = now, NewValuesJson = JsonSerializer.Serialize(new{command.SupplyId,command.PurchasedAtUtc,command.PurchasePresentation,command.PackageQuantity,command.UnitPrice,command.PaymentMethod,command.RenditionPerson}) });
            await db.SaveChangesAsync(ct); return Ok(purchase.Id);
        }
        catch (ArgumentException ex) { return Fail(ex.Message); }
        catch (DbUpdateConcurrencyException) { return Fail("El saldo cambió mientras se registraba la compra. Intenta nuevamente."); }
        catch (DbUpdateException) { return Fail("La compra no pudo guardarse. Verifica los datos e intenta nuevamente."); }
    }

    public async Task<InventoryResult> SaveRecipeAsync(SaveRecipeCommand command, Guid userId, CancellationToken ct = default)
    {
        if (command.Lines.Count == 0) return Fail("Añade al menos un insumo a la receta.");
        if (command.Lines.Any(x => x.RequiredQuantity <= 0)) return Fail("Todas las cantidades de la receta deben ser mayores que cero.");
        if (command.Lines.Any(x => x.SupplyId == Guid.Empty)) return Fail("Selecciona un insumo en todas las filas de la receta.");
        var duplicateSupplyId = command.Lines.GroupBy(x => x.SupplyId).FirstOrDefault(x => x.Count() > 1)?.Key;
        if (duplicateSupplyId is not null)
        {
            var duplicateName = await db.Supplies.AsNoTracking()
                .Where(x => x.Id == duplicateSupplyId.Value)
                .Select(x => x.Name)
                .SingleOrDefaultAsync(ct);
            return Fail($"{duplicateName ?? "El insumo seleccionado"} está repetido en la receta. Deja una sola fila y suma las cantidades.");
        }
        if (!await db.Products.AnyAsync(x => x.Id == command.ProductId && x.IsActive, ct)) return Fail("Selecciona un producto activo.");
        var validSupplies = await db.Supplies.CountAsync(x => command.Lines.Select(l => l.SupplyId).Contains(x.Id) && x.IsActive, ct);
        if (validSupplies != command.Lines.Count) return Fail("La receta contiene un insumo inexistente o inactivo.");
        var executionStrategy = db.Database.CreateExecutionStrategy();
        try
        {
            return await executionStrategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var now = clock.GetUtcNow();
                var current = await db.RecipeVersions.Where(x => x.ProductId == command.ProductId && x.EffectiveToUtc == null).ToListAsync(ct);
                foreach (var recipe in current) recipe.Close(now);
                var nextVersion = (await db.RecipeVersions.Where(x => x.ProductId == command.ProductId).MaxAsync(x => (int?)x.Version, ct) ?? 0) + 1;
                var created = RecipeVersion.Create(command.ProductId, nextVersion, command.Name, now, command.YieldQuantity);
                db.RecipeVersions.Add(created);
                db.RecipeIngredients.AddRange(command.Lines.Select(x => RecipeIngredient.Create(created.Id, x.SupplyId, x.RequiredQuantity)));
                db.AuditEntries.Add(new AuditEntry { Id = Guid.NewGuid(), Action = "RecipeSaved", EntityType = "RecipeVersion", EntityId = created.Id.ToString(), UserId = userId, OccurredAtUtc = now, NewValuesJson = JsonSerializer.Serialize(command), Reason = $"Receta v{nextVersion}" });
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return Ok(created.Id);
            });
        }
        catch (ArgumentException ex) { return Fail(ex.Message); }
    }

    public async Task<InventoryResult> RegisterSupplyCountAsync(SupplyCountCommand command, Guid userId, CancellationToken ct = default)
    {
        try
        {
            var state = await (from inventorySupply in db.Supplies
                               join existingBalance in db.SupplyBalances on inventorySupply.Id equals existingBalance.SupplyId into balances
                               from existingBalance in balances.DefaultIfEmpty()
                               where inventorySupply.Id == command.SupplyId && inventorySupply.IsActive
                               select new { Supply = inventorySupply, Balance = existingBalance }).SingleOrDefaultAsync(ct);
            if (state is null) return Fail("Selecciona un insumo activo.");
            var balance = state.Balance;
            if (balance is null) { balance = TentacionSana.Domain.Inventory.SupplyBalance.Create(command.SupplyId); db.SupplyBalances.Add(balance); }
            var now = clock.GetUtcNow();
            var count = SupplyStockCount.Create(command.SupplyId, balance.Quantity, command.CountedQuantity, command.Reason, userId, now);
            balance.Adjust(command.CountedQuantity); db.SupplyStockCounts.Add(count);
            if (count.Difference != 0) db.SupplyMovements.Add(SupplyMovement.Create(command.SupplyId, null, null,
                count.Difference > 0 ? SupplyMovementKind.PositiveAdjustment : SupplyMovementKind.NegativeAdjustment,
                count.Difference, null, command.Reason, userId, now));
            db.AuditEntries.Add(new AuditEntry { Id = Guid.NewGuid(), Action = "SupplyStockCounted", EntityType = "SupplyStockCount", EntityId = count.Id.ToString(), UserId = userId, OccurredAtUtc = now, NewValuesJson = JsonSerializer.Serialize(new { command.SupplyId, count.ExpectedQuantity, count.CountedQuantity, count.Difference }), Reason = command.Reason.Trim() });
            await db.SaveChangesAsync(ct); return Ok(count.Id);
        }
        catch (ArgumentException ex) { return Fail(ex.Message); }
        catch (DbUpdateConcurrencyException) { return Fail("El saldo cambió mientras se guardaba el ajuste. Intenta nuevamente."); }
        catch (DbUpdateException) { return Fail("El ajuste no pudo guardarse. Verifica los datos e intenta nuevamente."); }
    }

    public async Task<IReadOnlyList<StockItem>> GetStockAsync(CancellationToken ct = default) => await db.Products.AsNoTracking().Where(x => x.IsActive)
        .Select(x => new StockItem(x.Id, x.Name, db.ProductStockBalances.Where(s => s.ProductId == x.Id).Select(s => s.PhysicalQuantity).FirstOrDefault(), db.ProductStockBalances.Where(s => s.ProductId == x.Id).Select(s => s.ReservedQuantity).FirstOrDefault(), db.ProductStockBalances.Where(s => s.ProductId == x.Id).Select(s => s.PhysicalQuantity - s.ReservedQuantity).FirstOrDefault(), db.StockReservations.Where(r => r.ProductId == x.Id && r.IsActive).Sum(r => r.ShortageQuantity), db.ProductStockBalances.Where(s => s.ProductId == x.Id).Select(s => s.Version).FirstOrDefault())).ToListAsync(ct);
    public async Task<IReadOnlyList<BatchItem>> GetBatchesAsync(CancellationToken ct = default) => await BatchQuery().ToListAsync(ct);
    public async Task<IReadOnlyList<BatchItem>> GetRecentBatchesAsync(int count, CancellationToken ct = default) => await BatchQuery().Take(Math.Clamp(count, 1, 100)).ToListAsync(ct);
    public async Task<IReadOnlyList<MovementItem>> GetMovementsAsync(CancellationToken ct = default) => await db.InventoryMovements.AsNoTracking()
        .Where(x => x.ProductionBatchId == null ||
            !db.ProductionBatches.Any(batch => batch.Id == x.ProductionBatchId && batch.IsVoided))
        .OrderByDescending(x => x.OccurredAtUtc)
        .Select(x => new MovementItem(x.Id, db.Products.Where(p => p.Id == x.ProductId).Select(p => p.Name).First(), x.Kind.ToString(), x.Quantity, x.HistoricalTotalCost, x.Reason, x.OccurredAtUtc)).ToListAsync(ct);
    private IQueryable<BatchItem> BatchQuery() => db.ProductionBatches.AsNoTracking().Where(x => !x.IsVoided).OrderByDescending(x => x.ProducedAtUtc).Select(x => new BatchItem(x.Id, x.Number, db.Products.Where(p => p.Id == x.ProductId).Select(p => p.Name).First(), x.ProducedAtUtc, x.GoodUnits, x.WasteUnits, x.RemainingUnits, x.EstimatedUnitCost, db.Users.Where(u => u.Id == x.CreatedByUserId).Select(u => u.DisplayName).FirstOrDefault() ?? "—", x.Version));
    private async Task AssignShortagesAsync(Guid productId, ProductStockBalance balance, CancellationToken ct)
    {
        var reservations=await db.StockReservations.Where(x=>x.ProductId==productId&&x.IsActive&&x.ShortageQuantity>0).OrderBy(x=>x.CreatedAtUtc).ToListAsync(ct);
        foreach(var reservation in reservations){var assigned=balance.Reserve(reservation.ShortageQuantity);if(assigned==0)break;reservation.Allocate(reservation.ReservedQuantity+assigned);}
    }
    private async Task FreeReservedStockAsync(Guid productId,int reduction,ProductStockBalance balance,CancellationToken ct)
    {
        var needed=Math.Max(0,reduction-balance.AvailableQuantity);if(needed==0)return;
        var reservations=await db.StockReservations.Where(x=>x.ProductId==productId&&x.IsActive&&x.ReservedQuantity>0).OrderByDescending(x=>x.CreatedAtUtc).ToListAsync(ct);
        foreach(var reservation in reservations){var release=Math.Min(needed,reservation.ReservedQuantity);reservation.Allocate(reservation.ReservedQuantity-release);balance.Release(release);needed-=release;if(needed==0)break;}
        if(needed>0)throw new InvalidOperationException("No fue posible liberar las reservas necesarias para corregir el lote.");
    }
    private async Task AdjustRecipeConsumptionAsync(ProductionBatch batch,int producedDelta,string reason,Guid userId,DateTimeOffset now,CancellationToken ct)
    {
        if(producedDelta==0||batch.RecipeVersionId is null)return;
        var recipe=await db.RecipeVersions.SingleAsync(x=>x.Id==batch.RecipeVersionId,ct);
        var ingredients=await db.RecipeIngredients.Where(x=>x.RecipeVersionId==recipe.Id).ToListAsync(ct);
        foreach(var ingredient in ingredients)
        {
            var quantity=RecipeQuantities.Required(ingredient.RequiredQuantity,recipe.YieldQuantity,Math.Abs(producedDelta));
            var balance=await SupplyBalance(ingredient.SupplyId,ct);
            if(producedDelta>0)balance.Consume(quantity);else balance.Add(quantity);
            db.SupplyMovements.Add(SupplyMovement.Create(ingredient.SupplyId,batch.Id,null,SupplyMovementKind.ProductionCorrection,
                producedDelta>0?-quantity:quantity,null,$"Corrección del lote {batch.Number}: {reason}",userId,now));
        }
    }
    private async Task<bool> IsAdministrator(Guid userId){var user=await users.FindByIdAsync(userId.ToString());return user is not null&&(await users.IsInRoleAsync(user,AppRoles.Administrator)||await db.UserRoles.AnyAsync(x=>x.UserId==userId&&db.RoleClaims.Any(c=>c.RoleId==x.RoleId&&c.ClaimType==AppPermissions.ClaimType&&c.ClaimValue==AppPermissions.Administration)));}
    private async Task<ProductStockBalance> Balance(Guid productId, CancellationToken ct) { var balance = await db.ProductStockBalances.SingleOrDefaultAsync(x => x.ProductId == productId, ct); if (balance is not null) return balance; balance = ProductStockBalance.Create(productId); db.ProductStockBalances.Add(balance); return balance; }
    private async Task<SupplyBalance> SupplyBalance(Guid supplyId,CancellationToken ct){var balance=await db.SupplyBalances.SingleOrDefaultAsync(x=>x.SupplyId==supplyId,ct);if(balance is not null)return balance;balance=TentacionSana.Domain.Inventory.SupplyBalance.Create(supplyId);db.SupplyBalances.Add(balance);return balance;}
    private static string UnitText(MeasurementUnit unit)=>unit switch{MeasurementUnit.Unit=>"unid.",MeasurementUnit.Gram=>"g",MeasurementUnit.Milliliter=>"ml",_=>unit.ToString()};
    private static string MovementText(SupplyMovementKind kind)=>kind switch{SupplyMovementKind.Purchase=>"Compra",SupplyMovementKind.ProductionConsumption=>"Producción",SupplyMovementKind.PositiveAdjustment=>"Ajuste +",SupplyMovementKind.NegativeAdjustment=>"Ajuste −",SupplyMovementKind.ProductionCorrection=>"Corrección de producción",_=>kind.ToString()};
    private void Audit(string action, Guid id, Guid userId, DateTimeOffset now) => db.AuditEntries.Add(new AuditEntry { Id = Guid.NewGuid(), Action = action, EntityType = "Inventory", EntityId = id.ToString(), UserId = userId, OccurredAtUtc = now });
    private static InventoryResult Ok(Guid id) => new(true, id, []);
    private static InventoryResult Fail(string error) => new(false, null, [error]);
}
#pragma warning restore CA1725
