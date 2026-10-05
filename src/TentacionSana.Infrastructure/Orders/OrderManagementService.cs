using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Text.Json;
using TentacionSana.Application.Orders;
using TentacionSana.Application.Customers;
using TentacionSana.Application.Security;
using TentacionSana.Domain.Orders;
using TentacionSana.Domain.Deliveries;
using TentacionSana.Domain.Inventory;
using TentacionSana.Domain.Requests;
using TentacionSana.Infrastructure.Auditing;
using TentacionSana.Infrastructure.Identity;
using TentacionSana.Infrastructure.Persistence;

#pragma warning disable CA1725

namespace TentacionSana.Infrastructure.Orders;

public sealed class OrderManagementService(ApplicationDbContext db, TimeProvider clock, UserManager<ApplicationUser> users, IDeliveryPointImageService pointImages) : IOrderManagementService
{
    public async Task<OrderOperationResult> CreateDraftAsync(Guid customerId, Guid userId, CancellationToken ct = default)
    {
        if (!await db.Customers.AnyAsync(x => x.Id == customerId && x.IsActive, ct)) return Fail("El cliente no existe o está inactivo.");
        var now=clock.GetUtcNow(); var order=Order.CreateDraft(customerId,null,userId,now); db.Orders.Add(order); Audit("OrderCreated",order.Id,userId,now); await db.SaveChangesAsync(ct); return Ok(order.Id);
    }

    public async Task<OrderOperationResult> ConfigureDraftAsync(Guid id,int version,OrderConfiguration c,Guid user,CancellationToken ct=default)
    {
        var order=await Load(id,ct); if(order is null)return Fail("El pedido no existe."); if(order.Version!=version)return Conflict();
        if(!await ValidateRelations(c,ct))return Fail("La sucursal, contacto o responsable de pago no pertenece al cliente.");
        try { var dates=order.PromisedDateHistory.Count; var changes=order.ChangeHistory.Count; order.Configure(c.CustomerId,c.DeliveryPointId,c.ContactId,c.PaymentResponsiblePartyId,c.PromisedAtUtc,c.Notes,user,clock.GetUtcNow()); if(order.PromisedDateHistory.Count>dates)db.PromisedDateHistory.Add(order.PromisedDateHistory[^1]); foreach(var item in order.ChangeHistory.Skip(changes))db.OrderChangeHistory.Add(item); return await Save(order,"OrderConfigured",user,ct); } catch(Exception ex) when(ex is ArgumentException or InvalidOperationException){return Fail(ex.Message);}
    }

    public async Task<OrderOperationResult> AddLineAsync(Guid id,int version,OrderLineCommand c,Guid user,CancellationToken ct=default)
    {
        var order=await Load(id,ct); if(order is null)return Fail("El pedido no existe."); if(order.Version!=version)return Conflict();
        var now=clock.GetUtcNow(); var product=await db.Products.SingleOrDefaultAsync(x=>x.Id==c.ProductId&&x.IsActive,ct); if(product is null)return Fail("El producto no existe o está inactivo.");
        var standard=await db.ProductPrices.Where(x=>x.ProductId==c.ProductId&&x.EffectiveFromUtc<=now&&(x.EffectiveToUtc==null||x.EffectiveToUtc>now)).OrderByDescending(x=>x.EffectiveFromUtc).Select(x=>(decimal?)x.Amount).FirstOrDefaultAsync(ct); if(standard is null)return Fail("El producto no tiene precio vigente.");
        if(c.SoldUnitPrice<standard.Value){var actor=await users.FindByIdAsync(user.ToString());if(actor is null||!await users.IsInRoleAsync(actor,AppRoles.Administrator))return Fail("El descuento requiere autorización de una persona administradora.");}
        try
        {
            var line=order.AddLine(product.Id,product.Name,c.SaleQuantity,c.ReplacementQuantity,c.TastingQuantity,standard.Value,c.SoldUnitPrice,c.DiscountReason,c.ReplacementReason,c.ReplacementNotes,c.TastingReason,c.TastingNotes,user,now);
            db.OrderLines.Add(line);
            db.OrderChangeHistory.Add(order.ChangeHistory[^1]);
            var saved=await Save(order,"OrderLineAdded",user,ct);
            return saved.Succeeded?Ok(line.Id):saved;
        }
        catch(Exception ex) when(ex is ArgumentException or InvalidOperationException){return Fail(ex.Message);}
    }

    public async Task<OrderOperationResult> RemoveLineAsync(Guid id,Guid lineId,int version,Guid user,CancellationToken ct=default)
    {
        var order=await Load(id,ct); if(order is null)return Fail("El pedido no existe."); if(order.Version!=version)return Conflict();
        try { var line=order.Lines.SingleOrDefault(x=>x.Id==lineId); order.RemoveLine(lineId,user,clock.GetUtcNow()); if(line is not null)db.OrderLines.Remove(line); db.OrderChangeHistory.Add(order.ChangeHistory[^1]); return await Save(order,"OrderLineRemoved",user,ct); } catch(Exception ex) when(ex is ArgumentException or InvalidOperationException){return Fail(ex.Message);}
    }

    public async Task<OrderOperationResult> ConfirmAsync(Guid id,int version,Guid user,CancellationToken ct=default)
    {
        var strategy=db.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var tx=await db.Database.BeginTransactionAsync(ct);
                var order=await Load(id,ct);
                if(order is null)return Fail("El pedido no existe.");
                if(order.Status is OrderStatus.Confirmed or OrderStatus.InPreparation or OrderStatus.Ready or OrderStatus.OutForDelivery or OrderStatus.Delivered)return Ok(order.Id);
                if(order.Version!=version)return Conflict();
                var customer=await db.Customers.SingleAsync(x=>x.Id==order.CustomerId,ct);
                var point=order.DeliveryPointId is null?null:await db.DeliveryPoints.SingleAsync(x=>x.Id==order.DeliveryPointId,ct);
                var contact=order.ContactId is null?null:await db.CustomerContacts.SingleAsync(x=>x.Id==order.ContactId,ct);
                var payer=order.PaymentResponsiblePartyId is null?null:await db.PaymentResponsibleParties.SingleAsync(x=>x.Id==order.PaymentResponsiblePartyId,ct);
                var now=clock.GetUtcNow();
                var linesJson=JsonSerializer.Serialize(order.Lines.Where(x=>x.IsActive).Select(x=>new{x.ProductId,x.ProductName,x.SaleQuantity,x.ReplacementQuantity,x.TastingQuantity,x.Quantity,x.StandardUnitPrice,x.SoldUnitPrice,x.DiscountReason,x.ReplacementReason,x.ReplacementNotes,x.TastingReason,x.TastingNotes}));
                order.Confirm(OrderSnapshot.Create(order.Id,customer.Name,point?.Label,point?.Address,contact?.Name,contact?.Phone,payer?.Name,now,point?.Reference,point?.Location,linesJson),user,now);
                foreach(var group in order.Reservations.GroupBy(x=>x.ProductId))
                {
                    var balance=await db.ProductStockBalances.SingleOrDefaultAsync(x=>x.ProductId==group.Key,ct);
                    if(balance is null){balance=TentacionSana.Domain.Inventory.ProductStockBalance.Create(group.Key);db.ProductStockBalances.Add(balance);}
                    foreach(var reservation in group)reservation.Allocate(balance.Reserve(reservation.Quantity));
                }
                db.OrderSnapshots.Add(order.Snapshots[^1]);
                db.OrderStatusHistory.Add(order.StatusHistory[^1]);
                db.StockReservations.AddRange(order.Reservations);
                var result=await Save(order,"OrderConfirmed",user,ct);
                if(result.Succeeded)await tx.CommitAsync(ct);
                return result;
            });
        }
        catch(Exception ex) when(ex is ArgumentException or InvalidOperationException){return Fail(ex.Message);}
        catch(DbUpdateException){return Fail("No se pudo confirmar el pedido en la base de datos. Intenta nuevamente.");}
    }

    public async Task<OrderOperationResult> RegisterCompletedOrderAsync(Guid id,int version,OrderConfiguration configuration,DateTimeOffset completedAtUtc,string receiverName,Guid user,CancellationToken ct=default)
    {
        if(!await IsAdministrator(user)) return Fail("Solo una persona administradora puede registrar un pedido ya realizado.");
        if(completedAtUtc>clock.GetUtcNow()) return Fail("La fecha de un pedido ya realizado no puede ser futura.");
        if(string.IsNullOrWhiteSpace(receiverName)) return Fail("La persona receptora es obligatoria.");

        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        try
        {
            var order=await Load(id,ct);
            if(order is null) return Fail("El pedido no existe.");
            if(order.Version!=version) return Conflict();
            if(order.Status!=OrderStatus.Draft) return Fail("Solo se puede registrar como ya realizado un pedido pendiente.");
            if(!await ValidateRelations(configuration,ct)) return Fail("La sucursal, contacto o responsable de pago no pertenece al cliente.");

            var changes=order.ChangeHistory.Count;
            var promisedDates=order.PromisedDateHistory.Count;
            order.Configure(configuration.CustomerId,configuration.DeliveryPointId,configuration.ContactId,configuration.PaymentResponsiblePartyId,configuration.PromisedAtUtc,configuration.Notes,user,completedAtUtc);
            foreach(var change in order.ChangeHistory.Skip(changes)) db.OrderChangeHistory.Add(change);
            foreach(var date in order.PromisedDateHistory.Skip(promisedDates)) db.PromisedDateHistory.Add(date);

            var snapshot=await Snapshot(order,completedAtUtc,ct);
            order.Confirm(snapshot,user,completedAtUtc);
            db.OrderSnapshots.Add(snapshot);
            db.OrderStatusHistory.Add(order.StatusHistory[^1]);
            db.StockReservations.AddRange(order.Reservations);

            foreach(var group in order.Reservations.GroupBy(x=>x.ProductId))
            {
                var balance=await db.ProductStockBalances.SingleOrDefaultAsync(x=>x.ProductId==group.Key,ct);
                if(balance is null) { balance=ProductStockBalance.Create(group.Key); db.ProductStockBalances.Add(balance); }
                foreach(var reservation in group)
                {
                    reservation.Allocate(balance.Reserve(reservation.Quantity));
                    if(reservation.ShortageQuantity>0) throw new InvalidOperationException("No hay stock físico suficiente para registrar este pedido ya realizado.");
                }
            }

            decimal saleAmount=0;
            decimal? historicalCost=0;
            foreach(var line in order.Lines.Where(x=>x.IsActive))
            {
                var reservation=order.Reservations.Single(x=>x.OrderLineId==line.Id&&x.IsActive);
                var balance=await db.ProductStockBalances.SingleAsync(x=>x.ProductId==line.ProductId,ct);
                balance.DeliverReserved(line.Quantity);
                reservation.Consume(line.Quantity);
                var batches=await db.ProductionBatches.Where(x=>x.ProductId==line.ProductId&&x.RemainingUnits>0).OrderBy(x=>x.ProducedAtUtc).ThenBy(x=>x.Number).ToListAsync(ct);
                var components=new[]
                {
                    (Quantity:line.SaleQuantity,Kind:InventoryMovementKind.DeliveredSale,Reason:"Venta registrada como ya realizada."),
                    (Quantity:line.ReplacementQuantity,Kind:InventoryMovementKind.Replacement,Reason:"Reposición registrada como ya realizada."),
                    (Quantity:line.TastingQuantity,Kind:InventoryMovementKind.Tasting,Reason:"Degustación registrada como ya realizada.")
                };
                foreach(var component in components.Where(x=>x.Quantity>0))
                {
                    var movement=InventoryMovement.Create(line.ProductId,null,component.Kind,-component.Quantity,0,component.Reason,user,completedAtUtc);
                    db.InventoryMovements.Add(movement);
                    var remaining=component.Quantity;
                    foreach(var batch in batches.Where(x=>x.RemainingUnits>0))
                    {
                        var take=Math.Min(remaining,batch.RemainingUnits);
                        if(take==0) continue;
                        batch.Consume(take);
                        db.InventoryMovementAllocations.Add(InventoryMovementAllocation.Create(movement.Id,batch.Id,take,batch.EstimatedUnitCost));
                        if(component.Kind==InventoryMovementKind.DeliveredSale&&historicalCost is not null) historicalCost=batch.EstimatedUnitCost is null?null:historicalCost+take*batch.EstimatedUnitCost.Value;
                        remaining-=take;
                        if(remaining==0) break;
                    }
                    if(remaining>0) throw new InvalidOperationException("Los lotes no respaldan el inventario del pedido ya realizado.");
                }
                saleAmount+=line.SaleQuantity*line.SoldUnitPrice;
            }

            var delivery=Delivery.CreateHistoricallyCompleted(order.Id,completedAtUtc,receiverName,order.Lines.Where(x=>x.IsActive).Select(x=>(x.Id,x.SaleQuantity,x.ReplacementQuantity,x.TastingQuantity)),user);
            db.Deliveries.Add(delivery);
            order.AdvanceTo(OrderStatus.InPreparation,"Pedido registrado como ya realizado.",user,completedAtUtc);
            db.OrderStatusHistory.Add(order.StatusHistory[^1]);
            order.AdvanceTo(OrderStatus.Ready,"Pedido registrado como ya realizado.",user,completedAtUtc);
            db.OrderStatusHistory.Add(order.StatusHistory[^1]);
            order.MarkOutForDelivery(user,completedAtUtc);
            db.OrderStatusHistory.Add(order.StatusHistory[^1]);
            order.MarkDelivered(user,completedAtUtc);
            db.OrderStatusHistory.Add(order.StatusHistory[^1]);

            if(saleAmount>0)
            {
                db.Sales.Add(Sale.Create(order.Id,delivery.Id,saleAmount,historicalCost,completedAtUtc));
                var receivable=Receivable.Create(order.Id,order.CustomerId,order.PaymentResponsiblePartyId);
                receivable.AddSale(saleAmount);
                db.Receivables.Add(receivable);
            }

            Audit("HistoricalOrderCompleted",order.Id,user,clock.GetUtcNow(),"Pedido registrado como ya realizado.");
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Ok(order.Id);
        }
        catch(DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return Conflict();
        }
        catch(DbUpdateException)
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return Conflict();
        }
        catch(Exception exception) when(exception is ArgumentException or InvalidOperationException)
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return Fail(exception.Message);
        }
    }

    public async Task<OrderOperationResult> CancelAsync(Guid id,int version,string reason,Guid user,CancellationToken ct=default)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct); var order=await Load(id,ct); if(order is null)return Fail("El pedido no existe."); if(order.Version!=version)return Conflict();
        try
        {
            var now=clock.GetUtcNow();
            var releases=order.Reservations.Where(x=>x.IsActive&&x.ReservedQuantity>0).GroupBy(x=>x.ProductId).ToDictionary(x=>x.Key,x=>x.Sum(y=>y.ReservedQuantity));
            order.Cancel(reason,user,now);
            foreach(var item in releases){var balance=await db.ProductStockBalances.SingleAsync(x=>x.ProductId==item.Key,ct);balance.Release(item.Value);}
            db.OrderStatusHistory.Add(order.StatusHistory[^1]);

            var activeDeliveries=await db.Deliveries.Include(x=>x.History)
                .Where(x=>x.OrderId==id&&x.Status!=DeliveryStatus.Delivered&&x.Status!=DeliveryStatus.Cancelled)
                .ToListAsync(ct);
            if(activeDeliveries.Count>0)
            {
                var deliveryIds=activeDeliveries.Select(x=>x.Id).ToList();
                var routeStops=await db.DeliveryRouteStops.Where(x=>deliveryIds.Contains(x.DeliveryId)).ToListAsync(ct);
                db.DeliveryRouteStops.RemoveRange(routeStops);
                foreach(var delivery in activeDeliveries)
                {
                    delivery.Cancel($"Pedido cancelado: {reason}",user,now);
                    db.DeliveryStatusHistory.Add(delivery.History[^1]);
                }
            }

            var result=await Save(order,"OrderCancelled",user,ct,reason);if(result.Succeeded)await tx.CommitAsync(ct);return result;
        }
        catch(Exception ex) when(ex is ArgumentException or InvalidOperationException){return Fail(ex.Message);}
    }

    public async Task<OrderOperationResult> ReviseConfigurationAsync(Guid id,int version,OrderConfiguration c,string reason,Guid user,CancellationToken ct=default)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);var order=await Load(id,ct);if(order is null)return Fail("El pedido no existe.");if(order.Version!=version)return Conflict();if(!await CanModifyProgressed(order,user))return Fail("Este estado requiere autorización administrativa.");if(!await ValidateRelations(c,ct))return Fail("Las relaciones comerciales no pertenecen al cliente.");
        var changes=order.ChangeHistory.Count;var dates=order.PromisedDateHistory.Count;var now=clock.GetUtcNow();try{order.ReviseConfiguration(c.CustomerId,c.DeliveryPointId,c.ContactId,c.PaymentResponsiblePartyId,c.PromisedAtUtc,c.Notes,reason,user,now);foreach(var x in order.ChangeHistory.Skip(changes))db.OrderChangeHistory.Add(x);foreach(var x in order.PromisedDateHistory.Skip(dates))db.PromisedDateHistory.Add(x);var snapshot=await Snapshot(order,now,ct);order.Snapshots.Add(snapshot);db.OrderSnapshots.Add(snapshot);var result=await Save(order,"ConfirmedOrderConfigured",user,ct,reason);if(result.Succeeded)await tx.CommitAsync(ct);return result;}catch(Exception ex)when(ex is ArgumentException or InvalidOperationException){return Fail(ex.Message);}
    }

    public async Task<OrderOperationResult> DeleteDraftAsync(Guid id,int version,Guid user,CancellationToken ct=default)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var order=await Load(id,ct);
        if(order is null)return Fail("El pedido no existe.");
        if(order.Version!=version)return Conflict();
        if(order.Status!=OrderStatus.Draft)return Fail("Solo se pueden eliminar pedidos pendientes. Los pedidos confirmados deben cancelarse para conservar su historial.");

        if(order.SourceRequestId is { } requestId)
        {
            var request=await db.ProductRequests.Include(x=>x.StatusHistory).SingleOrDefaultAsync(x=>x.Id==requestId,ct);
            if(request is not null&&request.ConvertedOrderId==order.Id)
            {
                request.ReopenAfterOrderDeletion(order.Id,user,clock.GetUtcNow());
                db.RequestStatusHistory.Add(request.StatusHistory[^1]);
            }
        }

        db.Orders.Remove(order);
        Audit("OrderDeleted",order.Id,user,clock.GetUtcNow(),"Pedido pendiente eliminado.");
        try{await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);return Ok(id);}
        catch(DbUpdateConcurrencyException){return Conflict();}
    }

    public async Task<OrderOperationResult> ReviseLineAsync(Guid id,Guid lineId,int version,ConfirmedLineCommand c,Guid user,CancellationToken ct=default)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);var order=await Load(id,ct);if(order is null)return Fail("El pedido no existe.");if(order.Version!=version)return Conflict();if(!await CanModifyProgressed(order,user))return Fail("Este estado requiere autorización administrativa.");var line=order.Lines.SingleOrDefault(x=>x.Id==lineId);if(line is null)return Fail("La línea no existe.");if(c.SoldUnitPrice<line.StandardUnitPrice&&!await IsAdministrator(user))return Fail("El descuento requiere autorización de una persona administradora.");
        try{var reservation=order.Reservations.Single(x=>x.OrderLineId==lineId&&x.IsActive);var oldReserved=reservation.ReservedQuantity;var total=c.SaleQuantity+c.ReplacementQuantity+c.TastingQuantity;order.ReviseLine(lineId,c.SaleQuantity,c.ReplacementQuantity,c.TastingQuantity,line.StandardUnitPrice,c.SoldUnitPrice,c.DiscountReason,c.ReplacementReason,c.ReplacementNotes,c.TastingReason,c.TastingNotes,c.Reason,user,clock.GetUtcNow());var balance=await db.ProductStockBalances.SingleAsync(x=>x.ProductId==line.ProductId,ct);if(total<oldReserved){balance.Release(oldReserved-total);reservation.Allocate(total);}else if(total>oldReserved)reservation.Allocate(oldReserved+balance.Reserve(total-oldReserved));db.OrderChangeHistory.Add(order.ChangeHistory[^1]);var result=await Save(order,"ConfirmedOrderLineChanged",user,ct,c.Reason);if(result.Succeeded)await tx.CommitAsync(ct);return result;}catch(Exception ex)when(ex is ArgumentException or InvalidOperationException){return Fail(ex.Message);}
    }

    public async Task<OrderOperationResult> RemoveConfirmedLineAsync(Guid id,Guid lineId,int version,string reason,Guid user,CancellationToken ct=default)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);var order=await Load(id,ct);if(order is null)return Fail("El pedido no existe.");if(order.Version!=version)return Conflict();if(!await CanModifyProgressed(order,user))return Fail("Este estado requiere autorización administrativa.");try{var reservation=order.Reservations.Single(x=>x.OrderLineId==lineId&&x.IsActive);var release=reservation.ReservedQuantity;order.RemoveConfirmedLine(lineId,reason,user,clock.GetUtcNow());if(release>0){var balance=await db.ProductStockBalances.SingleAsync(x=>x.ProductId==reservation.ProductId,ct);balance.Release(release);}db.OrderChangeHistory.Add(order.ChangeHistory[^1]);var result=await Save(order,"ConfirmedOrderLineRemoved",user,ct,reason);if(result.Succeeded)await tx.CommitAsync(ct);return result;}catch(Exception ex)when(ex is ArgumentException or InvalidOperationException){return Fail(ex.Message);}
    }

    public async Task<OrderOperationResult> AdvanceStatusAsync(Guid id,int version,string nextStatus,string reason,Guid user,CancellationToken ct=default)
    {
        var order=await Load(id,ct);if(order is null)return Fail("El pedido no existe.");if(order.Version!=version)return Conflict();if(!Enum.TryParse<OrderStatus>(nextStatus,true,out var next))return Fail("El estado solicitado no es válido.");if(order.Status!=OrderStatus.Confirmed&&!await IsAdministrator(user))return Fail("Este cambio de estado requiere autorización administrativa.");try{order.AdvanceTo(next,reason,user,clock.GetUtcNow());db.OrderStatusHistory.Add(order.StatusHistory[^1]);return await Save(order,"OrderStatusChanged",user,ct,reason);}catch(Exception ex)when(ex is ArgumentException or InvalidOperationException){return Fail(ex.Message);}
    }

    public async Task<OrderDetail?> GetAsync(Guid id,CancellationToken ct=default)
    {
        var o=await db.Orders.AsNoTracking().AsSplitQuery().Include(x=>x.Lines).Include(x=>x.StatusHistory).Include(x=>x.ChangeHistory).Include(x=>x.PromisedDateHistory).Include(x=>x.Reservations).SingleOrDefaultAsync(x=>x.Id==id,ct); if(o is null)return null;
        var customer=await db.Customers.Where(x=>x.Id==o.CustomerId).Select(x=>x.Name).SingleAsync(ct);
        var point=o.DeliveryPointId is null?null:await db.DeliveryPoints.Where(x=>x.Id==o.DeliveryPointId).Select(x=>x.Label).SingleOrDefaultAsync(ct);
        var contact=o.ContactId is null?null:await db.CustomerContacts.Where(x=>x.Id==o.ContactId).Select(x=>x.Name).SingleOrDefaultAsync(ct);
        var delivery=await db.Deliveries.AsNoTracking().Where(x=>x.OrderId==id).OrderByDescending(x=>x.ScheduledAtUtc).Select(x=>new { x.Id,Status=x.Status.ToString(), Driver=x.DriverUserId==null?null:db.Users.Where(u=>u.Id==x.DriverUserId).Select(u=>u.DisplayName).FirstOrDefault() }).FirstOrDefaultAsync(ct);
        var deliveryIds=await db.Deliveries.AsNoTracking().Where(x=>x.OrderId==id).Select(x=>x.Id).ToListAsync(ct);
        var deliveryHistory=await db.DeliveryStatusHistory.AsNoTracking().Where(x=>deliveryIds.Contains(x.DeliveryId)).Select(x=>new OrderHistoryItem(x.ChangedAtUtc,"Entrega",$"{x.PreviousStatus} → {x.NewStatus}: {x.Reason}")).ToListAsync(ct);
        var receivable=await db.Receivables.AsNoTracking().Where(x=>x.OrderId==id).Select(x=>new { x.InvoicedAmount,x.PaidAmount,x.Version }).SingleOrDefaultAsync(ct);
        var payments=await db.Payments.AsNoTracking().Where(x=>x.OrderId==id&&x.Status==Domain.Deliveries.PaymentStatus.Confirmed).OrderBy(x=>x.ReceivedAtUtc).Select(x=>new { Method=x.Method.ToString(),x.Amount,x.ReceivedAtUtc }).ToListAsync(ct);
        var paymentMethod=PaymentMethod(payments.Select(x=>x.Method)); var paid=receivable?.PaidAmount??payments.Sum(x=>x.Amount); var orderTotal=o.Lines.Where(x=>x.IsActive).Sum(x=>x.LineTotal); var balance=Math.Max(0,orderTotal-paid);
        var history=new[]{new OrderHistoryItem(o.CreatedAtUtc,"Pedido","Pedido creado")}.Concat(o.StatusHistory.Select(x=>new OrderHistoryItem(x.ChangedAtUtc,"Estado",$"{x.PreviousStatus} → {x.NewStatus}: {x.Reason}")))
            .Concat(o.ChangeHistory.Select(x=>new OrderHistoryItem(x.ChangedAtUtc,"Cambio",$"{x.Field}: {x.PreviousValue} → {x.NewValue}")))
            .Concat(o.PromisedDateHistory.Select(x=>new OrderHistoryItem(x.ChangedAtUtc,"Fecha prometida",$"{x.PreviousDateUtc:g} → {x.NewDateUtc:g}")))
            .Concat(deliveryHistory).Concat(payments.Select(x=>new OrderHistoryItem(x.ReceivedAtUtc,"Pago",$"Pago registrado por {PaymentMethodText(x.Method)} · Bs {x.Amount:0.00}"))).OrderByDescending(x=>x.AtUtc).ToList();
        var lines=o.Lines.Where(x=>x.IsActive).Select(x=>{var r=o.Reservations.FirstOrDefault(r=>r.OrderLineId==x.Id&&r.IsActive);return new OrderLineDetail(x.Id,x.ProductId,x.ProductName,x.SaleQuantity,x.ReplacementQuantity,x.TastingQuantity,x.Quantity,x.StandardUnitPrice,x.SoldUnitPrice,x.UnitDiscount,x.DiscountReason,x.ReplacementReason,x.ReplacementNotes,x.TastingReason,x.TastingNotes,r?.ReservedQuantity??0,r?.ShortageQuantity??0);}).ToList();
        return new(o.Id,o.Number,o.CustomerId,o.DeliveryPointId,o.ContactId,o.PaymentResponsiblePartyId,customer,point,contact,delivery?.Driver,delivery?.Status,o.Status.ToString(),PaymentStatus(paid,balance,payments.Count),paymentMethod,paid,balance,receivable?.Version??1,o.PromisedAtUtc,o.Notes,o.Version,lines,history,o.Reservations.Where(x=>x.IsActive).Sum(x=>x.ShortageQuantity));
    }
    public async Task<IReadOnlyList<OrderListItem>> ListAsync(CancellationToken ct=default)
    {
        var orders=await db.Orders.AsNoTracking().OrderByDescending(x=>x.CreatedAtUtc).Select(x=>new {x.Id,x.Number,x.CustomerId,x.DeliveryPointId,x.ContactId,Status=x.Status.ToString(),x.PromisedAtUtc,x.Notes,x.CreatedAtUtc,x.Version,Total=x.Lines.Where(l=>l.IsActive).Sum(l=>l.SoldUnitPrice*l.SaleQuantity),Sale=x.Lines.Where(l=>l.IsActive).Sum(l=>l.SaleQuantity),Replacement=x.Lines.Where(l=>l.IsActive).Sum(l=>l.ReplacementQuantity),Tasting=x.Lines.Where(l=>l.IsActive).Sum(l=>l.TastingQuantity),Quantity=x.Lines.Where(l=>l.IsActive).Sum(l=>l.Quantity),Products=x.Lines.Where(l=>l.IsActive).Select(l=>l.ProductName).ToList()}).ToListAsync(ct);
        var ids=orders.Select(x=>x.Id).ToList(); var customerIds=orders.Select(x=>x.CustomerId).Distinct().ToList(); var pointIds=orders.Where(x=>x.DeliveryPointId!=null).Select(x=>x.DeliveryPointId!.Value).Distinct().ToList();
        var customers=await db.Customers.AsNoTracking().Where(x=>customerIds.Contains(x.Id)).ToDictionaryAsync(x=>x.Id,x=>x.Name,ct);
        var points=await db.DeliveryPoints.AsNoTracking().Where(x=>pointIds.Contains(x.Id)).ToDictionaryAsync(x=>x.Id,x=>new{x.Label,x.Location,x.ContactId},ct);
        var contactIds=orders.Where(x=>x.ContactId!=null).Select(x=>x.ContactId!.Value).Concat(points.Values.Where(x=>x.ContactId!=null).Select(x=>x.ContactId!.Value)).Distinct().ToList();
        var contacts=await db.CustomerContacts.AsNoTracking().Where(x=>contactIds.Contains(x.Id)).ToDictionaryAsync(x=>x.Id,x=>x.Phone,ct);
        var receivables=await db.Receivables.AsNoTracking().Where(x=>ids.Contains(x.OrderId)).ToDictionaryAsync(x=>x.OrderId,x=>new {x.InvoicedAmount,x.PaidAmount},ct);
        var payments=await db.Payments.AsNoTracking().Where(x=>ids.Contains(x.OrderId)&&x.Status==Domain.Deliveries.PaymentStatus.Confirmed).Select(x=>new {x.OrderId,Method=x.Method.ToString(),x.Amount,x.PaymentDateUtc}).ToListAsync(ct);
        var deliveries=await db.Deliveries.AsNoTracking().Where(x=>ids.Contains(x.OrderId)).SelectMany(x=>x.Lines.Select(l=>new {x.OrderId,l.DeliveredQuantity,l.DeliveredSaleQuantity,l.DeliveredReplacementQuantity,l.DeliveredTastingQuantity})).ToListAsync(ct);
        var methods=payments.GroupBy(x=>x.OrderId).ToDictionary(x=>x.Key,x=>PaymentMethod(x.Select(y=>y.Method))); var paymentCounts=payments.GroupBy(x=>x.OrderId).ToDictionary(x=>x.Key,x=>x.Count());
        var today=clock.GetLocalNow().Date; var todayPayments=payments.Where(x=>x.PaymentDateUtc.ToLocalTime().Date==today).GroupBy(x=>x.OrderId).ToDictionary(x=>x.Key,x=>new {QrCount=x.Count(y=>y.Method=="Qr"),QrAmount=x.Where(y=>y.Method=="Qr").Sum(y=>y.Amount),CashCount=x.Count(y=>y.Method=="Cash"),CashAmount=x.Where(y=>y.Method=="Cash").Sum(y=>y.Amount)});
        var delivered=deliveries.GroupBy(x=>x.OrderId).ToDictionary(x=>x.Key,x=>new{Total=x.Sum(y=>y.DeliveredQuantity),Sale=x.Sum(y=>y.DeliveredSaleQuantity),Replacement=x.Sum(y=>y.DeliveredReplacementQuantity),Tasting=x.Sum(y=>y.DeliveredTastingQuantity)});
        var imageUrls=new Dictionary<Guid,string?>();foreach(var pointId in pointIds)imageUrls[pointId]=await pointImages.GetUrlAsync(pointId,ct);
        return orders.Select(x=>{receivables.TryGetValue(x.Id,out var r);todayPayments.TryGetValue(x.Id,out var tp);points.TryGetValue(x.DeliveryPointId??Guid.Empty,out var point);delivered.TryGetValue(x.Id,out var done);var contactId=x.ContactId??point?.ContactId;var paid=r?.PaidAmount??payments.Where(p=>p.OrderId==x.Id).Sum(p=>p.Amount);var balance=Math.Max(0,x.Total-paid);var closed=x.Status is "Delivered" or "Cancelled";var pendingSale=closed?0:Math.Max(0,x.Sale-(done?.Sale??0));var pendingReplacement=closed?0:Math.Max(0,x.Replacement-(done?.Replacement??0));var pendingTasting=closed?0:Math.Max(0,x.Tasting-(done?.Tasting??0));var pending=pendingSale+pendingReplacement+pendingTasting;return new OrderListItem(x.Id,x.Number,customers.GetValueOrDefault(x.CustomerId,"—"),point?.Label,x.DeliveryPointId is null?null:imageUrls.GetValueOrDefault(x.DeliveryPointId.Value),contactId is null?null:contacts.GetValueOrDefault(contactId.Value),point?.Location,x.Status,PaymentStatus(paid,balance,paymentCounts.GetValueOrDefault(x.Id)),methods.GetValueOrDefault(x.Id),tp?.QrCount??0,tp?.QrAmount??0,tp?.CashCount??0,tp?.CashAmount??0,x.PromisedAtUtc,x.Sale,x.Replacement,x.Tasting,x.Quantity,pendingSale,pendingReplacement,pendingTasting,pending,x.Notes,x.Total,paid,balance,x.CreatedAtUtc,x.Version,x.Products);}).ToList();
    }
    public async Task<IReadOnlyList<OrderOption>> CustomerOptionsAsync(CancellationToken ct=default)=>await db.Customers.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.Name).Select(x=>new OrderOption(x.Id,x.Name,x.DeliveryPoints.Where(y=>y.IsActive).Select(y=>new OrderPointOption(y.Id,y.Label,y.Address,y.Reference,y.Location,y.ContactId,y.PaymentResponsiblePartyId)).ToList(),x.Contacts.Where(y=>y.IsActive).Select(y=>new OrderSubOption(y.Id,y.Name,y.Phone)).ToList(),x.PaymentResponsibleParties.Where(y=>y.IsActive).Select(y=>new OrderSubOption(y.Id,y.Label??y.Name,y.Phone)).ToList())).ToListAsync(ct);
    public async Task<IReadOnlyList<ProductOption>> ProductOptionsAsync(CancellationToken ct=default){var now=clock.GetUtcNow();return await db.Products.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.Name).Select(x=>new ProductOption(x.Id,x.Name,db.ProductPrices.Where(p=>p.ProductId==x.Id&&p.EffectiveFromUtc<=now&&(p.EffectiveToUtc==null||p.EffectiveToUtc>now)).OrderByDescending(p=>p.EffectiveFromUtc).Select(p=>p.Amount).FirstOrDefault())).ToListAsync(ct);}

    private Task<Order?> Load(Guid id,CancellationToken ct)=>db.Orders.AsSplitQuery().Include(x=>x.Lines).Include(x=>x.Reservations).Include(x=>x.ChangeHistory).Include(x=>x.StatusHistory).Include(x=>x.PromisedDateHistory).SingleOrDefaultAsync(x=>x.Id==id,ct);
    private async Task<bool> ValidateRelations(OrderConfiguration c,CancellationToken ct)=>await db.Customers.AnyAsync(x=>x.Id==c.CustomerId&&x.IsActive,ct)&&(c.DeliveryPointId is null||await db.DeliveryPoints.AnyAsync(x=>x.Id==c.DeliveryPointId&&x.CustomerId==c.CustomerId&&x.IsActive,ct))&&(c.ContactId is null||await db.CustomerContacts.AnyAsync(x=>x.Id==c.ContactId&&x.CustomerId==c.CustomerId&&x.IsActive,ct))&&(c.PaymentResponsiblePartyId is null||await db.PaymentResponsibleParties.AnyAsync(x=>x.Id==c.PaymentResponsiblePartyId&&x.CustomerId==c.CustomerId&&x.IsActive,ct));
    private async Task<OrderSnapshot> Snapshot(Order order,DateTimeOffset now,CancellationToken ct){var customer=await db.Customers.SingleAsync(x=>x.Id==order.CustomerId,ct);var point=order.DeliveryPointId is null?null:await db.DeliveryPoints.SingleAsync(x=>x.Id==order.DeliveryPointId,ct);var contact=order.ContactId is null?null:await db.CustomerContacts.SingleAsync(x=>x.Id==order.ContactId,ct);var payer=order.PaymentResponsiblePartyId is null?null:await db.PaymentResponsibleParties.SingleAsync(x=>x.Id==order.PaymentResponsiblePartyId,ct);var linesJson=JsonSerializer.Serialize(order.Lines.Where(x=>x.IsActive).Select(x=>new{x.ProductId,x.ProductName,x.SaleQuantity,x.ReplacementQuantity,x.TastingQuantity,x.Quantity,x.StandardUnitPrice,x.SoldUnitPrice,x.DiscountReason,x.ReplacementReason,x.ReplacementNotes,x.TastingReason,x.TastingNotes}));return OrderSnapshot.Create(order.Id,customer.Name,point?.Label,point?.Address,contact?.Name,contact?.Phone,payer?.Name,now,point?.Reference,point?.Location,linesJson);}
    private async Task<bool> CanModifyProgressed(Order order,Guid user)=>order.Status==OrderStatus.Confirmed||await IsAdministrator(user);
    private async Task<bool> IsAdministrator(Guid user){var actor=await users.FindByIdAsync(user.ToString());return actor is not null&&(await users.IsInRoleAsync(actor,AppRoles.Administrator)||await db.UserRoles.AnyAsync(x=>x.UserId==user&&db.RoleClaims.Any(c=>c.RoleId==x.RoleId&&c.ClaimType==AppPermissions.ClaimType&&c.ClaimValue==AppPermissions.Administration)));}
    private async Task<OrderOperationResult> Save(Order o,string action,Guid user,CancellationToken ct,string? reason=null){Audit(action,o.Id,user,clock.GetUtcNow(),reason);try{await db.SaveChangesAsync(ct);return Ok(o.Id);}catch(DbUpdateConcurrencyException){return Conflict();}}
    private void Audit(string action,Guid id,Guid user,DateTimeOffset now,string? reason=null)=>db.AuditEntries.Add(new AuditEntry{Id=Guid.NewGuid(),UserId=user,Action=action,EntityType="Order",EntityId=id.ToString(),Reason=reason,OccurredAtUtc=now});
    private static OrderOperationResult Ok(Guid id)=>new(true,id,[]); private static OrderOperationResult Fail(string e)=>new(false,null,[e]); private static OrderOperationResult Conflict()=>Fail("El pedido fue modificado por otra sesión. Recarga antes de continuar.");
    private static string PaymentStatus(decimal paid,decimal balance,int paymentCount)=>paymentCount==0?"Pending":balance>0?"Partial":"Paid";
    private static string? PaymentMethod(IEnumerable<string> methods){var distinct=methods.Distinct().ToList();return distinct.Count switch{0=>null,1=>distinct[0],_=>"Mixed"};}
    private static string PaymentMethodText(string method)=>method switch{"Qr"=>"QR","Cash"=>"efectivo","Transfer"=>"transferencia",_=>method};
}
#pragma warning restore CA1725
