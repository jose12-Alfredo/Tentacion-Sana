using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using TentacionSana.Application.Deliveries;
using TentacionSana.Application.Security;
using TentacionSana.Domain.Deliveries;
using TentacionSana.Domain.Finance;
using TentacionSana.Domain.Inventory;
using TentacionSana.Domain.Orders;
using TentacionSana.Infrastructure.Auditing;
using TentacionSana.Infrastructure.Idempotency;
using TentacionSana.Infrastructure.Media;
using TentacionSana.Infrastructure.Persistence;

namespace TentacionSana.Infrastructure.Deliveries;

public sealed class DeliveryService(ApplicationDbContext db, TimeProvider clock, IOptions<CloudinaryOptions> cloudinaryOptions, LocationCoordinateResolver coordinateResolver, IHttpClientFactory httpClientFactory) : IDeliveryService
{
    public async Task<DeliveryResult> ScheduleAsync(ScheduleDeliveryCommand command, Guid userId, CancellationToken cancellationToken = default)
    {
        var order = await db.Orders.Include(x => x.Lines).Include(x => x.Reservations).SingleOrDefaultAsync(x => x.Id == command.OrderId, cancellationToken);
        if (order is null || order.Status is not (OrderStatus.Confirmed or OrderStatus.InPreparation or OrderStatus.Ready or OrderStatus.OutForDelivery)) return Fail("Solo un pedido pendiente admite entregas.");
        if (command.DriverUserId is { } driver && !await CanBeAssignedAsync(driver, userId, cancellationToken)) return Fail("Selecciona un repartidor habilitado o asígnate la entrega a ti mismo.");

        var activeStatuses = new[] { DeliveryStatus.Scheduled, DeliveryStatus.Assigned, DeliveryStatus.InRoute, DeliveryStatus.Partial };
        var requestedLineIds = command.Lines.Select(x => x.OrderLineId).Distinct().ToList();
        var scheduledByLine = await (from deliveryLine in db.DeliveryLines.AsNoTracking()
                                     join delivery in db.Deliveries.AsNoTracking() on deliveryLine.DeliveryId equals delivery.Id
                                     where requestedLineIds.Contains(deliveryLine.OrderLineId)
                                     group new { deliveryLine, delivery } by deliveryLine.OrderLineId into grouped
                                     select new
                                     {
                                         LineId = grouped.Key,
                                         AlreadyScheduled = grouped.Sum(x => activeStatuses.Contains(x.delivery.Status) ? x.deliveryLine.AssignedQuantity - x.deliveryLine.DeliveredQuantity : 0),
                                         DeliveredSale = grouped.Sum(x => x.deliveryLine.DeliveredSaleQuantity),
                                         PendingSale = grouped.Sum(x => activeStatuses.Contains(x.delivery.Status) ? x.deliveryLine.AssignedSaleQuantity - x.deliveryLine.DeliveredSaleQuantity : 0),
                                         DeliveredReplacement = grouped.Sum(x => x.deliveryLine.DeliveredReplacementQuantity),
                                         PendingReplacement = grouped.Sum(x => activeStatuses.Contains(x.delivery.Status) ? x.deliveryLine.AssignedReplacementQuantity - x.deliveryLine.DeliveredReplacementQuantity : 0)
                                     })
            .ToDictionaryAsync(x => x.LineId, cancellationToken);
        var splitLines = new List<(Guid lineId, int saleQuantity, int replacementQuantity, int tastingQuantity)>();
        foreach (var requested in command.Lines)
        {
            var line = order.Lines.SingleOrDefault(x => x.Id == requested.OrderLineId && x.IsActive);
            var reservation = order.Reservations.SingleOrDefault(x => x.OrderLineId == requested.OrderLineId && x.IsActive);
            if (line is null || reservation is null || requested.Quantity < 1) return Fail("La línea o cantidad programada no es válida.");
            scheduledByLine.TryGetValue(requested.OrderLineId, out var scheduled);
            if (requested.Quantity > reservation.ReservedQuantity - (scheduled?.AlreadyScheduled ?? 0)) return Fail($"La cantidad de {line.ProductName} supera las unidades reservadas disponibles para programar.");
            var saleQuantity = Math.Min(requested.Quantity, Math.Max(0, line.SaleQuantity - (scheduled?.DeliveredSale ?? 0) - (scheduled?.PendingSale ?? 0)));
            var replacementQuantity=Math.Min(requested.Quantity-saleQuantity,Math.Max(0,line.ReplacementQuantity-(scheduled?.DeliveredReplacement ?? 0)-(scheduled?.PendingReplacement ?? 0)));
            splitLines.Add((line.Id,saleQuantity,replacementQuantity,requested.Quantity-saleQuantity-replacementQuantity));
        }

        try
        {
            var now = clock.GetUtcNow();
            var delivery = Delivery.Create(order.Id, command.DriverUserId, command.ScheduledAtUtc, splitLines, userId, now);
            if (order.Status == OrderStatus.Confirmed)
            {
                order.AdvanceTo(OrderStatus.InPreparation, "Entrega programada.", userId, now);
                db.OrderStatusHistory.Add(order.StatusHistory[^1]);
            }
            if (order.Status == OrderStatus.InPreparation)
            {
                order.AdvanceTo(OrderStatus.Ready, "Entrega programada.", userId, now);
                db.OrderStatusHistory.Add(order.StatusHistory[^1]);
            }
            db.Deliveries.Add(delivery);
            Audit("DeliveryScheduled", delivery.Id, userId, now);
            await db.SaveChangesAsync(cancellationToken);
            return Ok(delivery.Id);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) { return Fail(exception.Message); }
    }

    public async Task<DeliveryResult> AssignAsync(Guid deliveryId, int expectedVersion, Guid driverUserId, string reason, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await CanBeAssignedAsync(driverUserId, userId, cancellationToken)) return Fail("Selecciona un repartidor habilitado o asígnate la entrega a ti mismo.");
        var delivery = await LoadAsync(deliveryId, cancellationToken);
        if (delivery is null) return Fail("La entrega no existe.");
        if (delivery.Version != expectedVersion) return Conflict();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = clock.GetUtcNow();
            var route = await db.DeliveryRoutes.Include(x => x.Stops)
                .SingleOrDefaultAsync(x => x.Stops.Any(stop => stop.DeliveryId == delivery.Id), cancellationToken);
            var order = await db.Orders.Include(x => x.StatusHistory).SingleAsync(x => x.Id == delivery.OrderId, cancellationToken);
            if (route is not null)
            {
                if (route.Stops.Count == 1) db.DeliveryRoutes.Remove(route);
                else route.RemoveStop(delivery.Id);
            }
            if (order.Status == OrderStatus.OutForDelivery)
            {
                order.ReturnToReady(userId, now);
                db.OrderStatusHistory.Add(order.StatusHistory[^1]);
            }
            delivery.Assign(driverUserId, reason, userId, now);
            db.DeliveryStatusHistory.Add(delivery.History[^1]);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(deliveryId);
        }
        catch (DbUpdateConcurrencyException) { await transaction.RollbackAsync(cancellationToken); db.ChangeTracker.Clear(); return Conflict(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateException) { await transaction.RollbackAsync(cancellationToken); db.ChangeTracker.Clear(); return Fail(exception.Message); }
    }

    public async Task<DeliveryResult> StartAsync(Guid deliveryId, int expectedVersion, Guid userId, CancellationToken cancellationToken = default)
    {
        var delivery = await LoadAsync(deliveryId, cancellationToken);
        if (delivery is null) return Fail("La entrega no existe.");
        if (!await CanOperateAsync(delivery, userId, cancellationToken)) return Fail("Solo el repartidor asignado puede operar esta entrega.");
        if (delivery.Version != expectedVersion) return Conflict();
        try
        {
            delivery.Start(userId, clock.GetUtcNow());
            db.DeliveryStatusHistory.Add(delivery.History[^1]);
            var order = await db.Orders.Include(x => x.StatusHistory).SingleAsync(x => x.Id == delivery.OrderId, cancellationToken);
            if (order.Status == OrderStatus.Ready) { order.MarkOutForDelivery(userId, clock.GetUtcNow()); db.OrderStatusHistory.Add(order.StatusHistory[^1]); }
            await db.SaveChangesAsync(cancellationToken);
            return Ok(deliveryId);
        }
        catch (DbUpdateConcurrencyException) { return Conflict(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) { return Fail(exception.Message); }
    }

    public async Task<DeliveryResult> ReturnOrderToPendingAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await CanCoordinateRoutesAsync(userId, cancellationToken)) return Fail("Solo el equipo de ventas o administraciÃ³n puede devolver una entrega a pendiente.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var delivery = await db.Deliveries.Include(x => x.Lines).Include(x => x.History)
                .Where(x => x.OrderId == orderId && (x.Status == DeliveryStatus.InRoute || x.Status == DeliveryStatus.Assigned))
                .OrderByDescending(x => x.ScheduledAtUtc).FirstOrDefaultAsync(cancellationToken);
            if (delivery is null) return Fail("El pedido no tiene una entrega asignada o en reparto.");
            if (delivery.Lines.Any(x => x.DeliveredQuantity > 0)) return Fail("No se puede volver atrÃ¡s porque la entrega ya tiene cantidades confirmadas.");

            var route = await db.DeliveryRoutes.Include(x => x.Stops)
                .SingleOrDefaultAsync(x => x.Stops.Any(stop => stop.DeliveryId == delivery.Id), cancellationToken);
            var order = await db.Orders.Include(x => x.StatusHistory).SingleAsync(x => x.Id == orderId, cancellationToken);
            var now = clock.GetUtcNow();
            if (delivery.Status == DeliveryStatus.InRoute)
            {
                delivery.ReturnToAssigned(userId, now);
                db.DeliveryStatusHistory.Add(delivery.History[^1]);
            }
            if (order.Status == OrderStatus.OutForDelivery)
            {
                order.ReturnToReady(userId, now);
                db.OrderStatusHistory.Add(order.StatusHistory[^1]);
            }
            if (route is not null)
            {
                if (route.Stops.Count == 1) db.DeliveryRoutes.Remove(route);
                else route.RemoveStop(delivery.Id);
            }
            Audit("DeliveryReturnedToPending", delivery.Id, userId, now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(delivery.Id);
        }
        catch (DbUpdateConcurrencyException) { await transaction.RollbackAsync(cancellationToken); db.ChangeTracker.Clear(); return Conflict(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken); db.ChangeTracker.Clear(); return Fail(exception.Message);
        }
    }

    public async Task<DeliveryResult> CompleteAsync(CompleteDeliveryCommand command, Guid userId, CancellationToken cancellationToken = default)
    {
        var executionStrategy = db.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(
            () => CompleteTransactionalAsync(command, userId, cancellationToken));
    }

    private async Task<DeliveryResult> CompleteTransactionalAsync(CompleteDeliveryCommand command, Guid userId, CancellationToken cancellationToken)
    {
        if (!ValidKey(command.IdempotencyKey)) return Fail("La clave de idempotencia es obligatoria.");
        var existing = await FindIdempotentAsync("CompleteDelivery", command.IdempotencyKey, cancellationToken);
        if (existing) return Ok(command.DeliveryId);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var delivery = await LoadAsync(command.DeliveryId, cancellationToken);
            if (delivery is null) return Fail("La entrega no existe.");
            if (!await CanOperateAsync(delivery, userId, cancellationToken)) return Fail("Solo el repartidor asignado puede operar esta entrega.");
            if (delivery.Version != command.ExpectedVersion) return Conflict();
            var order = await db.Orders.Include(x => x.Lines).Include(x => x.Reservations).Include(x => x.StatusHistory).SingleAsync(x => x.Id == delivery.OrderId, cancellationToken);
            var delivered = command.Lines.GroupBy(x => x.DeliveryLineId).ToDictionary(
                x => x.Key,
                x => (SaleQuantity: x.Sum(y => y.SaleQuantity), ReplacementQuantity: x.Sum(y => y.ReplacementQuantity), TastingQuantity: x.Sum(y => y.TastingQuantity)));
            if (delivered.Keys.Except(delivery.Lines.Select(x => x.Id)).Any()) return Fail("Una línea de entrega no pertenece al pedido.");
            var now = clock.GetUtcNow();
            decimal saleAmount = 0;
            decimal? historicalCost = 0;

            var productIds = delivery.Lines
                .Select(x => order.Lines.Single(line => line.Id == x.OrderLineId).ProductId)
                .Distinct()
                .ToList();
            var balances = await db.ProductStockBalances
                .Where(x => productIds.Contains(x.ProductId))
                .ToDictionaryAsync(x => x.ProductId, cancellationToken);
            var batchesByProduct = (await db.ProductionBatches
                    .Where(x => productIds.Contains(x.ProductId) && x.RemainingUnits > 0)
                    .OrderBy(x => x.ProducedAtUtc)
                    .ThenBy(x => x.Number)
                    .ToListAsync(cancellationToken))
                .GroupBy(x => x.ProductId)
                .ToDictionary(x => x.Key, x => x.ToList());

            foreach (var deliveryLine in delivery.Lines)
            {
                var requested = delivered.GetValueOrDefault(deliveryLine.Id);
                var quantity = requested.SaleQuantity + requested.ReplacementQuantity + requested.TastingQuantity;
                if (quantity == 0) continue;
                if (requested.SaleQuantity < 0 || requested.ReplacementQuantity < 0 || requested.TastingQuantity < 0 || requested.SaleQuantity > deliveryLine.PendingSaleQuantity || requested.ReplacementQuantity > deliveryLine.PendingReplacementQuantity || requested.TastingQuantity > deliveryLine.PendingTastingQuantity)
                    throw new InvalidOperationException("La cantidad entregada supera la pendiente por tipo.");
                var orderLine = order.Lines.Single(x => x.Id == deliveryLine.OrderLineId);
                var reservation = order.Reservations.Single(x => x.OrderLineId == orderLine.Id && x.IsActive);
                if (!balances.TryGetValue(orderLine.ProductId, out var balance))
                    throw new InvalidOperationException("El producto no tiene un saldo de inventario.");
                balance.DeliverReserved(quantity);
                reservation.Consume(quantity);
                var batches = batchesByProduct.GetValueOrDefault(orderLine.ProductId) ?? [];
                var components = new[]
                {
                    (Quantity: requested.SaleQuantity, Kind: InventoryMovementKind.DeliveredSale, Reason: "Venta entregada."),
                    (Quantity: requested.ReplacementQuantity, Kind: InventoryMovementKind.Replacement, Reason: "Reposición entregada."),
                    (Quantity: requested.TastingQuantity, Kind: InventoryMovementKind.Tasting, Reason: "Degustación entregada.")
                };
                foreach (var component in components.Where(x => x.Quantity > 0))
                {
                    var movement = InventoryMovement.Create(orderLine.ProductId, null, component.Kind, -component.Quantity, 0, component.Reason, userId, now);
                    db.InventoryMovements.Add(movement);
                    var remaining = component.Quantity;
                    foreach (var batch in batches.Where(x => x.RemainingUnits > 0))
                    {
                        var take = Math.Min(remaining, batch.RemainingUnits);
                        if (take == 0) continue;
                        batch.Consume(take);
                        db.InventoryMovementAllocations.Add(InventoryMovementAllocation.Create(movement.Id, batch.Id, take, batch.EstimatedUnitCost));
                        if (component.Kind == InventoryMovementKind.DeliveredSale && historicalCost is not null)
                            historicalCost = batch.EstimatedUnitCost is null ? null : historicalCost + take * batch.EstimatedUnitCost.Value;
                        remaining -= take;
                        if (remaining == 0) break;
                    }
                    if (remaining > 0) throw new InvalidOperationException("Los lotes no respaldan la entrega.");
                }
                saleAmount += requested.SaleQuantity * orderLine.SoldUnitPrice;
            }

            if (delivered.Values.Sum(x => x.SaleQuantity + x.ReplacementQuantity + x.TastingQuantity) <= 0) throw new InvalidOperationException("Debe confirmar al menos una unidad entregada.");
            delivery.Complete(delivered, command.ReceiverName, userId, now);
            db.DeliveryStatusHistory.Add(delivery.History[^1]);
            var receivable = await db.Receivables.SingleOrDefaultAsync(x => x.OrderId == order.Id, cancellationToken);
            if (saleAmount > 0)
            {
                var sale = Sale.Create(order.Id, delivery.Id, saleAmount, historicalCost, now);
                db.Sales.Add(sale);
                if (receivable is null) { receivable = Receivable.Create(order.Id, order.CustomerId, order.PaymentResponsiblePartyId); db.Receivables.Add(receivable); }
                receivable.AddSale(saleAmount);
            }
            if (command.PaymentAmount > 0) throw new InvalidOperationException("Registra el pago por separado para adjuntar su respaldo obligatorio.");
            if (order.Reservations.All(x => !x.IsActive) && order.Status == OrderStatus.OutForDelivery) { order.MarkDelivered(userId, now); db.OrderStatusHistory.Add(order.StatusHistory[^1]); }
            await CompleteRouteIfNeededAsync(delivery.Id, delivery.Status == DeliveryStatus.Delivered, userId, now, cancellationToken);
            db.IdempotencyRecords.Add(Idempotency("CompleteDelivery", command.IdempotencyKey, now));
            Audit("DeliveryCompleted", delivery.Id, userId, now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(delivery.Id);
        }
        catch (DbUpdateConcurrencyException) { await transaction.RollbackAsync(cancellationToken); db.ChangeTracker.Clear(); return await WaitForIdempotentAsync("CompleteDelivery", command.IdempotencyKey, cancellationToken) ? Ok(command.DeliveryId) : Conflict(); }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken); db.ChangeTracker.Clear();
            return await WaitForIdempotentAsync("CompleteDelivery", command.IdempotencyKey, cancellationToken) ? Ok(command.DeliveryId) : Fail("La entrega compitió con otra operación; recarga e intenta nuevamente.");
        }
        catch (ArgumentException exception) { await transaction.RollbackAsync(cancellationToken); return Fail(exception.Message); }
        catch (InvalidOperationException exception) { await transaction.RollbackAsync(cancellationToken); db.ChangeTracker.Clear(); return await WaitForIdempotentAsync("CompleteDelivery", command.IdempotencyKey, cancellationToken) ? Ok(command.DeliveryId) : Fail(exception.Message); }
    }

    public async Task<DeliveryResult> FailAsync(Guid deliveryId, int expectedVersion, string reason, Guid userId, CancellationToken cancellationToken = default)
    {
        var delivery = await LoadAsync(deliveryId, cancellationToken);
        if (delivery is null) return Fail("La entrega no existe.");
        if (!await CanOperateAsync(delivery, userId, cancellationToken)) return Fail("Solo el repartidor asignado puede operar esta entrega.");
        if (delivery.Version != expectedVersion) return Conflict();
        try { delivery.Fail(reason, userId, clock.GetUtcNow()); db.DeliveryStatusHistory.Add(delivery.History[^1]); await db.SaveChangesAsync(cancellationToken); return Ok(deliveryId); }
        catch (DbUpdateConcurrencyException) { return Conflict(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) { return Fail(exception.Message); }
    }

    public async Task<DeliveryResult> RegisterPaymentAsync(RegisterPaymentCommand command, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!ValidKey(command.IdempotencyKey)) return Fail("La clave de idempotencia es obligatoria.");
        if (command.EvidenceContent is null || string.IsNullOrWhiteSpace(command.EvidenceFileName) || command.EvidenceLength <= 0) return Fail("Debes adjuntar un respaldo para registrar este pago.");
        if (command.PaymentMethod is not ("Cash" or "Qr")) return Fail("El método de pago debe ser efectivo o QR.");
        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
        if (string.IsNullOrWhiteSpace(command.EvidenceContentType) || !allowedTypes.Contains(command.EvidenceContentType.ToLowerInvariant()) || command.EvidenceLength > 10 * 1024 * 1024) return Fail("El respaldo debe ser una imagen JPG, PNG o WebP de hasta 10 MB.");
        if (await FindIdempotentAsync("RegisterPayment", command.IdempotencyKey, cancellationToken)) return Ok(command.OrderId);
        if (!await db.Orders.AsNoTracking().AnyAsync(x => x.Id == command.OrderId && x.ArchivedAtUtc == null, cancellationToken))
            return Fail("No se pueden registrar pagos en un pedido eliminado de la vista.");
        var settings=cloudinaryOptions.Value;
        if(string.IsNullOrWhiteSpace(settings.CloudName)||string.IsNullOrWhiteSpace(settings.ApiKey)||string.IsNullOrWhiteSpace(settings.ApiSecret))return Fail("Cloudinary no está configurado.");
        var cloudinary=new Cloudinary(new Account(settings.CloudName,settings.ApiKey,settings.ApiSecret)){Api={Secure=true}};
        var upload=await cloudinary.UploadAsync(new ImageUploadParams{File=new FileDescription(Path.GetFileName(command.EvidenceFileName),command.EvidenceContent),Folder=$"tentacion-sana/pagos/{command.OrderId:N}",Type="authenticated",UniqueFilename=true,Overwrite=false,UseFilename=true},cancellationToken);
        if(upload.Error is not null||string.IsNullOrWhiteSpace(upload.PublicId))return Fail(upload.Error?.Message??"No se pudo guardar el respaldo del pago.");
        var executionStrategy = db.Database.CreateExecutionStrategy();
        try
        {
            return await executionStrategy.ExecuteAsync(async () =>
            {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            if (!await db.Orders.AsNoTracking().AnyAsync(x => x.Id == command.OrderId && x.ArchivedAtUtc == null, cancellationToken))
                return Fail("No se pueden registrar pagos en un pedido eliminado de la vista.");
            var receivable = await db.Receivables.SingleOrDefaultAsync(x => x.OrderId == command.OrderId, cancellationToken);
            if (receivable is null && command.IsAdvancePayment)
            {
                var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == command.OrderId && x.Status != OrderStatus.Cancelled, cancellationToken);
                if (order is null) return Fail("El pedido no existe o está cancelado.");
                receivable = Receivable.Create(order.Id, order.CustomerId, order.PaymentResponsiblePartyId); db.Receivables.Add(receivable);
            }
            if (receivable is null) return Fail("El pedido todavía no tiene una cuenta por cobrar.");
            if (receivable.Version != command.ExpectedReceivableVersion) return Conflict();
            var now = clock.GetUtcNow();
            if (command.DeliveryId is { } deliveryId && !await db.Deliveries.AnyAsync(x => x.Id == deliveryId && x.OrderId == command.OrderId, cancellationToken)) return Fail("La entrega no pertenece al pedido.");
            var payment=AddPayment(command.OrderId, command.DeliveryId, receivable, command.Amount, command.PaymentMethod, command.ExternalReference, command.CollectedByCurrentUser, userId, now, command.IsAdvancePayment,command.Notes,command.PaymentDateUtc);
            db.PaymentEvidence.Add(PaymentEvidence.Create(payment.Id,upload.PublicId,command.EvidenceFileName,upload.Format??string.Empty,upload.Bytes,userId,now));
            db.CashMovements.Add(CashMovement.Create(command.PaymentMethod=="Qr"?CashAccount.Bank:CashAccount.Cash,CashDirection.Income,CashSource.CustomerPayment,
                command.PaymentDateUtc??now,$"Pago de cliente",command.Amount,upload.PublicId,command.EvidenceFileName,upload.Format??string.Empty,upload.Bytes,userId,now,paymentId:payment.Id,category:"Cobro pedido"));
            db.IdempotencyRecords.Add(Idempotency("RegisterPayment", command.IdempotencyKey, now));
            Audit("PaymentRegistered", command.OrderId, userId, now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(command.OrderId);
            });
        }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return await WaitForIdempotentAsync("RegisterPayment", command.IdempotencyKey, cancellationToken) ? Ok(command.OrderId) : Conflict(); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return await WaitForIdempotentAsync("RegisterPayment", command.IdempotencyKey, cancellationToken) ? Ok(command.OrderId) : Conflict(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) { return Fail(exception.Message); }
    }

    public async Task<DeliveryResult> PlanRouteAsync(PlanDeliveryRouteCommand command, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await CanCoordinateRoutesAsync(userId, cancellationToken)) return Fail("Solo el equipo de ventas o administración puede organizar rutas.");
        if (!await CanBeAssignedAsync(command.DriverUserId, userId, cancellationToken)) return Fail("Selecciona un repartidor habilitado o asígnate la ruta a ti mismo.");
        var orderIds = command.OrderIds.Distinct().ToList();
        if (orderIds.Count == 0) return Fail("Selecciona al menos un pedido para la ruta.");

        var reusableStatuses = new[] { DeliveryStatus.Scheduled, DeliveryStatus.Assigned, DeliveryStatus.Partial, DeliveryStatus.Failed };
        var reusableDeliveries = await db.Deliveries.Include(x => x.Lines).Include(x => x.History)
            .Where(x => orderIds.Contains(x.OrderId) && reusableStatuses.Contains(x.Status))
            .ToListAsync(cancellationToken);
        var reusableDeliveryIds = reusableDeliveries.Select(x => x.Id).ToList();
        var alreadyRoutedIds = reusableDeliveryIds.Count == 0
            ? []
            : await db.DeliveryRouteStops.AsNoTracking()
                .Where(x => reusableDeliveryIds.Contains(x.DeliveryId))
                .Select(x => x.DeliveryId)
                .ToListAsync(cancellationToken);
        var reusableByOrder = reusableDeliveries
            .Where(x => !alreadyRoutedIds.Contains(x.Id) && x.Lines.Sum(line => line.PendingQuantity) > 0)
            .GroupBy(x => x.OrderId)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(delivery => DeliveryPriority(delivery.Status))
                    .ThenByDescending(delivery => delivery.ScheduledAtUtc)
                    .First());
        var ordersNeedingDelivery = orderIds.Where(x => !reusableByOrder.ContainsKey(x)).ToList();
        var options = (await SchedulableOrdersAsync(cancellationToken))
            .Where(x => ordersNeedingDelivery.Contains(x.Id))
            .ToDictionary(x => x.Id);
        if (options.Count != ordersNeedingDelivery.Count) return Fail("Uno de los pedidos ya no está disponible para incluirse en una ruta.");

        var points = await db.Orders.AsNoTracking()
            .Where(x => orderIds.Contains(x.Id))
            .Select(x => new { x.Id, x.DeliveryPointId, x.PromisedAtUtc })
            .ToListAsync(cancellationToken);
        if (points.Count != orderIds.Count) return Fail("Uno de los pedidos ya no existe.");
        var pointIds = points.Where(x => x.DeliveryPointId is not null).Select(x => x.DeliveryPointId!.Value).Distinct().ToList();
        var locations = await db.DeliveryPoints.AsNoTracking().Where(x => pointIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Location, cancellationToken);
        foreach (var item in points)
        {
            if (item.PromisedAtUtc is null || item.PromisedAtUtc.Value.ToLocalTime().Date > command.PromisedDate.ToDateTime(TimeOnly.MinValue)) return Fail("No se puede incluir un pedido cuya fecha prometida sea posterior a la fecha de la ruta.");
            if (item.DeliveryPointId is null || string.IsNullOrWhiteSpace(locations.GetValueOrDefault(item.DeliveryPointId.Value))) return Fail("Cada pedido necesita una ubicación configurada antes de incluirse en una ruta.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var deliveryIds = new List<Guid>();
            var now = clock.GetUtcNow();
            foreach (var orderId in orderIds)
            {
                if (reusableByOrder.TryGetValue(orderId, out var existingDelivery))
                {
                    if (existingDelivery.DriverUserId != command.DriverUserId || existingDelivery.Status != DeliveryStatus.Assigned)
                    {
                        existingDelivery.Assign(command.DriverUserId, "Asignación al preparar la ruta.", userId, now);
                        db.DeliveryStatusHistory.Add(existingDelivery.History[^1]);
                    }
                    deliveryIds.Add(existingDelivery.Id);
                    continue;
                }

                var option = options[orderId];
                var scheduled = await ScheduleAsync(new ScheduleDeliveryCommand(orderId, command.DriverUserId, option.PromisedAtUtc!.Value, option.Lines.Select(x => new ScheduleLine(x.Id, x.AvailableToSchedule)).ToList()), userId, cancellationToken);
                if (!scheduled.Succeeded || scheduled.Id is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    db.ChangeTracker.Clear();
                    return new(false, null, scheduled.Errors);
                }
                deliveryIds.Add(scheduled.Id.Value);
            }

            var route = DeliveryRoute.Create(command.DriverUserId, StartOfLocalDay(command.PromisedDate), deliveryIds, userId, now);
            db.DeliveryRoutes.Add(route);
            AuditRoute("DeliveryRoutePlanned", route.Id, userId, now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(route.Id);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return Conflict();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return Fail(exception.Message);
        }
    }

    public async Task<DeliveryResult> StartRouteAsync(Guid routeId, int expectedVersion, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await CanCoordinateRoutesAsync(userId, cancellationToken)) return Fail("Solo el equipo de ventas o administración puede iniciar rutas.");
        var route = await db.DeliveryRoutes.Include(x => x.Stops).SingleOrDefaultAsync(x => x.Id == routeId, cancellationToken);
        if (route is null) return Fail("La ruta no existe.");
        if (route.Version != expectedVersion) return Conflict();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var deliveryIds = route.Stops.Select(x => x.DeliveryId).ToList();
            var deliveries = await db.Deliveries.Include(x => x.History).Where(x => deliveryIds.Contains(x.Id)).ToListAsync(cancellationToken);
            if (deliveries.Count != deliveryIds.Count || deliveries.Any(x => x.DriverUserId != route.DriverUserId || x.Status != DeliveryStatus.Assigned)) return Fail("La ruta contiene una entrega que ya no está lista para salir.");
            var orders = await db.Orders.Include(x => x.StatusHistory).Where(x => deliveries.Select(d => d.OrderId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
            var now = clock.GetUtcNow();
            route.Start(now);
            foreach (var delivery in deliveries)
            {
                delivery.Start(userId, now);
                db.DeliveryStatusHistory.Add(delivery.History[^1]);
                var order = orders[delivery.OrderId];
                if (order.Status == OrderStatus.Ready)
                {
                    order.MarkOutForDelivery(userId, now);
                    db.OrderStatusHistory.Add(order.StatusHistory[^1]);
                }
            }
            AuditRoute("DeliveryRouteStarted", route.Id, userId, now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(route.Id);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return Conflict();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return Fail(exception.Message);
        }
    }

    public async Task<DeliveryResult> ReturnRouteToPendingAsync(Guid routeId, int expectedVersion, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await CanCoordinateRoutesAsync(userId, cancellationToken)) return Fail("Solo el equipo de ventas o administración puede quitar el estado en reparto.");
        var route = await db.DeliveryRoutes.Include(x => x.Stops).SingleOrDefaultAsync(x => x.Id == routeId, cancellationToken);
        if (route is null) return Fail("La ruta no existe.");
        if (route.Version != expectedVersion) return Conflict();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var deliveryIds = route.Stops.Select(x => x.DeliveryId).ToList();
            var deliveries = await db.Deliveries.Include(x => x.History).Where(x => deliveryIds.Contains(x.Id)).ToListAsync(cancellationToken);
            var expectedDeliveryStatus = route.Status == DeliveryRouteStatus.Planned ? DeliveryStatus.Assigned : DeliveryStatus.InRoute;
            if (route.Status is not (DeliveryRouteStatus.Planned or DeliveryRouteStatus.InRoute) ||
                deliveries.Count != deliveryIds.Count || deliveries.Any(x => x.Status != expectedDeliveryStatus || x.Lines.Any(line => line.DeliveredQuantity > 0)))
                return Fail("No se puede quitar el estado porque alguna parada ya tiene una entrega confirmada.");
            var orders = await db.Orders.Include(x => x.StatusHistory).Where(x => deliveries.Select(d => d.OrderId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
            var now = clock.GetUtcNow();
            foreach (var delivery in deliveries)
            {
                if (delivery.Status == DeliveryStatus.InRoute)
                {
                    delivery.ReturnToAssigned(userId, now);
                    db.DeliveryStatusHistory.Add(delivery.History[^1]);
                }
                var order = orders[delivery.OrderId];
                if (order.Status == OrderStatus.OutForDelivery)
                {
                    order.ReturnToReady(userId, now);
                    db.OrderStatusHistory.Add(order.StatusHistory[^1]);
                }
            }
            var removedRouteId = route.Id;
            db.DeliveryRoutes.Remove(route);
            AuditRoute("DeliveryRouteRemovedFromDispatch", removedRouteId, userId, now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(removedRouteId);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return Conflict();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return Fail(exception.Message);
        }
    }

    public async Task<DeliveryResult> ReorderRouteAsync(ReorderDeliveryRouteCommand command, Guid userId, CancellationToken cancellationToken = default)
    {
        var route = await db.DeliveryRoutes.Include(x => x.Stops).SingleOrDefaultAsync(x => x.Id == command.RouteId, cancellationToken);
        if (route is null) return Fail("La ruta no existe.");
        if (route.DriverUserId != userId && !await CanCoordinateRoutesAsync(userId, cancellationToken)) return Fail("Solo el repartidor asignado o el equipo de coordinación puede ordenar esta ruta.");
        if (route.Version != command.ExpectedVersion) return Conflict();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.DeliveryRouteStops.Where(x => x.DeliveryRouteId == route.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Position, x => x.Position + 1000), cancellationToken);
            route.Reorder(command.DeliveryIds);
            foreach (var stop in route.Stops)
            {
                var position = db.Entry(stop).Property(x => x.Position);
                position.OriginalValue += 1000;
                position.IsModified = true;
            }
            AuditRoute("DeliveryRouteReordered", route.Id, userId, clock.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(route.Id);
        }
        catch (DbUpdateConcurrencyException) { await transaction.RollbackAsync(cancellationToken); return Conflict(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateException) { await transaction.RollbackAsync(cancellationToken); return Fail(exception.Message); }
    }

    public async Task<DeliveryBoard> GetBoardAsync(DateOnly referenceDate, CancellationToken cancellationToken = default)
    {
        var start = StartOfLocalDay(referenceDate);
        var end = start.AddDays(1);
        var deliveredToday = await db.Deliveries.AsNoTracking()
            .CountAsync(x => x.Status == DeliveryStatus.Delivered && x.CompletedAtUtc >= start && x.CompletedAtUtc < end
                && db.Orders.Any(order => order.Id == x.OrderId && order.ArchivedAtUtc == null), cancellationToken);
        var collectedToday = await db.Payments.AsNoTracking()
            .Where(x => x.DeliveryId != null && x.Status == PaymentStatus.Confirmed && x.ReceivedAtUtc >= start && x.ReceivedAtUtc < end
                && db.Orders.Any(order => order.Id == x.OrderId && order.ArchivedAtUtc == null))
            .SumAsync(x => x.Amount, cancellationToken);
        var orders = await db.Orders.AsNoTracking().Include(x => x.Lines).Include(x => x.Reservations)
            .Where(x => x.ArchivedAtUtc == null && x.Status != OrderStatus.Cancelled && x.Status != OrderStatus.Delivered)
            .OrderBy(x => x.PromisedAtUtc == null)
            .ThenBy(x => x.PromisedAtUtc)
            .ThenBy(x => x.Number)
            .ToListAsync(cancellationToken);
        if (orders.Count == 0) return new DeliveryBoard(referenceDate, [], deliveredToday, collectedToday);

        var orderIds = orders.Select(x => x.Id).ToList();
        var customerIds = orders.Select(x => x.CustomerId).Distinct().ToList();
        var pointIds = orders.Where(x => x.DeliveryPointId is not null).Select(x => x.DeliveryPointId!.Value).Distinct().ToList();
        var contactIds = orders.Where(x => x.ContactId is not null).Select(x => x.ContactId!.Value).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking().Where(x => customerIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var points = await db.DeliveryPoints.AsNoTracking().Where(x => pointIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var coordinateTasks = points.Values.ToDictionary(
            x => x.Id,
            x => coordinateResolver.ResolveAsync(x.Location, x.Address, x.Reference, cancellationToken));
        await Task.WhenAll(coordinateTasks.Values);
        var contacts = await db.CustomerContacts.AsNoTracking().Where(x => contactIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var deliveries = await db.Deliveries.AsNoTracking().Include(x => x.Lines).Where(x => orderIds.Contains(x.OrderId)).ToListAsync(cancellationToken);
        var deliveryIds = deliveries.Select(x => x.Id).ToList();
        var routeStops = deliveryIds.Count == 0 ? [] : await db.DeliveryRouteStops.AsNoTracking().Where(x => deliveryIds.Contains(x.DeliveryId)).ToListAsync(cancellationToken);
        var routeIds = routeStops.Select(x => x.DeliveryRouteId).Distinct().ToList();
        var routes = routeIds.Count == 0 ? new Dictionary<Guid, DeliveryRoute>() : await db.DeliveryRoutes.AsNoTracking().Where(x => routeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var driverIds = deliveries.Where(x => x.DriverUserId is not null).Select(x => x.DriverUserId!.Value).Concat(routes.Values.Select(x => x.DriverUserId)).Distinct().ToList();
        var drivers = driverIds.Count == 0 ? new Dictionary<Guid, string>() : await db.Users.AsNoTracking().Where(x => driverIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);
        var paidAllocations = await db.PaymentAllocations.AsNoTracking().Where(x => orderIds.Contains(x.OrderId) && db.Payments.Any(payment => payment.Id == x.PaymentId && payment.Status == PaymentStatus.Confirmed)).ToListAsync(cancellationToken);
        var paidByOrder = paidAllocations.GroupBy(x => x.OrderId).ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));
        var items = new List<DeliveryBoardItem>();
        foreach (var order in orders)
        {
            var orderDeliveries = deliveries.Where(x => x.OrderId == order.Id).ToList();
            var tracked = orderDeliveries.Where(x => x.Status is not (DeliveryStatus.Delivered or DeliveryStatus.Cancelled))
                .OrderByDescending(x => DeliveryPriority(x.Status)).ThenByDescending(x => x.ScheduledAtUtc).FirstOrDefault();
            var latest = tracked ?? orderDeliveries.OrderByDescending(x => x.CompletedAtUtc ?? x.ScheduledAtUtc).FirstOrDefault();
            var deliveredByLine = orderDeliveries.SelectMany(x => x.Lines).GroupBy(x => x.OrderLineId).ToDictionary(x => x.Key, x => (Sale: x.Sum(y => y.DeliveredSaleQuantity), Replacement: x.Sum(y => y.DeliveredReplacementQuantity), Tasting: x.Sum(y => y.DeliveredTastingQuantity)));
            // La visibilidad del tablero depende de lo que falta entregar en el pedido completo,
            // incluso si una entrega activa contiene solo una parte de sus unidades.
            var pending = PendingFromReservations(order, deliveredByLine);
            var point = order.DeliveryPointId is { } pointId ? points.GetValueOrDefault(pointId) : null;
            var coordinate = point is null ? null : await coordinateTasks[point.Id];
            var contactId = order.ContactId ?? point?.ContactId;
            var contact = contactId is { } id ? contacts.GetValueOrDefault(id) : null;
            var routeStop = latest is null ? null : routeStops.FirstOrDefault(x => x.DeliveryId == latest.Id);
            var route = routeStop is null ? null : routes.GetValueOrDefault(routeStop.DeliveryRouteId);
            var status = tracked is null ? (order.Status == OrderStatus.Delivered ? "Delivered" : "Pending") : BoardStatus(tracked.Status);
            var total = order.Lines.Where(x => x.IsActive).Sum(x => x.SaleQuantity * x.SoldUnitPrice);
            var paid = paidByOrder.GetValueOrDefault(order.Id);
            var balance = Math.Max(0, total - paid);
            var totalPending = pending.Sale + pending.Replacement + pending.Tasting;
            if (totalPending == 0) continue;
            var deliveryCanJoinRoute = tracked is null ||
                (routeStop is null && tracked.Status is DeliveryStatus.Scheduled or DeliveryStatus.Assigned or DeliveryStatus.Partial or DeliveryStatus.Failed);
            var canAdd = deliveryCanJoinRoute &&
                (order.Status is OrderStatus.Confirmed or OrderStatus.InPreparation or OrderStatus.Ready or OrderStatus.OutForDelivery) &&
                totalPending > 0 && !string.IsNullOrWhiteSpace(point?.Location);
            var hasLocation = Uri.TryCreate(point?.Location, UriKind.Absolute, out var locationUri) && locationUri.Scheme is "http" or "https";
            items.Add(new DeliveryBoardItem(order.Id, latest?.Id, routeStop?.DeliveryRouteId, routeStop?.Position, order.Number, customers.GetValueOrDefault(order.CustomerId, "Cliente"), point?.Label, order.DeliveryPointId, point?.Address, point?.Reference, point?.Location, coordinate?.Latitude, coordinate?.Longitude, contact?.Name, contact?.Phone, pending.Sale, pending.Replacement, pending.Tasting, balance, status, latest?.DriverUserId, latest?.DriverUserId is { } driverId ? drivers.GetValueOrDefault(driverId) : null, latest?.Version ?? 0, canAdd, hasLocation, latest?.CompletedAtUtc, order.PromisedAtUtc, total, paid));
        }
        return new DeliveryBoard(referenceDate, items, deliveredToday, collectedToday);
    }

    public async Task<DeliveryRouteDetail?> GetRouteAsync(Guid routeId, CancellationToken cancellationToken = default)
    {
        var route = await db.DeliveryRoutes.AsNoTracking().Include(x => x.Stops).SingleOrDefaultAsync(x => x.Id == routeId, cancellationToken);
        if (route is null) return null;
        var driver = await db.Users.AsNoTracking().Where(x => x.Id == route.DriverUserId).Select(x => x.DisplayName).SingleOrDefaultAsync(cancellationToken) ?? "Repartidor";
        var board = await GetBoardAsync(DateOnly.FromDateTime(route.PromisedDateUtc.ToLocalTime().DateTime), cancellationToken);
        var byDelivery = board.Items.Where(x => x.DeliveryId is not null).ToDictionary(x => x.DeliveryId!.Value);
        var stops = route.Stops.Where(stop => byDelivery.ContainsKey(stop.DeliveryId)).OrderBy(x => x.Position).Select((stop, index) =>
        {
            byDelivery.TryGetValue(stop.DeliveryId, out var item);
            return new DeliveryRouteStopItem(stop.Id, stop.DeliveryId, item!.OrderNumber, item.Customer, item.DeliveryPoint, item.DeliveryPointId, item.Address, item.Reference, item.Location, item.Latitude, item.Longitude, item.SalePending, item.ReplacementPending, item.TastingPending, item.Balance, item.Status, index + 1);
        }).ToList();
        return new DeliveryRouteDetail(route.Id, route.DriverUserId, driver, route.Status.ToString(), route.Version, route.StartedAtUtc, stops);
    }

    public async Task<DeliveryRouteDetail?> GetCurrentRouteAsync(Guid driverUserId, CancellationToken cancellationToken = default)
    {
        var activeStatuses = new[] { DeliveryStatus.Scheduled, DeliveryStatus.Assigned, DeliveryStatus.InRoute, DeliveryStatus.Partial, DeliveryStatus.Failed };
        var routeId = await db.DeliveryRoutes.AsNoTracking()
            .Where(x => x.DriverUserId == driverUserId &&
                x.Status != DeliveryRouteStatus.Completed &&
                x.Stops.Any(stop => db.Deliveries.Any(delivery =>
                    delivery.Id == stop.DeliveryId &&
                    activeStatuses.Contains(delivery.Status) &&
                    delivery.Lines.Any(line => line.AssignedQuantity > line.DeliveredQuantity) &&
                    db.Orders.Any(order => order.Id == delivery.OrderId && order.ArchivedAtUtc == null && order.Status != OrderStatus.Cancelled))))
            .OrderByDescending(x => x.StartedAtUtc ?? x.CreatedAtUtc)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return routeId is null ? null : await GetRouteAsync(routeId.Value, cancellationToken);
    }

    public async Task<DeliveryRouteDetail?> PrepareCurrentRouteAsync(Guid driverUserId, CancellationToken cancellationToken = default)
    {
        var current = await GetCurrentRouteAsync(driverUserId, cancellationToken);
        if (current is not null) return current;

        var activeStatuses = new[] { DeliveryStatus.Scheduled, DeliveryStatus.Assigned, DeliveryStatus.InRoute, DeliveryStatus.Partial, DeliveryStatus.Failed };
        var deliveries = await db.Deliveries.AsNoTracking()
            .Where(x => x.DriverUserId == driverUserId && activeStatuses.Contains(x.Status) &&
                db.Orders.Any(order => order.Id == x.OrderId && order.ArchivedAtUtc == null && order.Status != OrderStatus.Cancelled) &&
                x.Lines.Any(line => line.AssignedQuantity > line.DeliveredQuantity) &&
                !db.DeliveryRouteStops.Any(stop => stop.DeliveryId == x.Id))
            .OrderBy(x => x.ScheduledAtUtc)
            .ThenBy(x => db.Orders.Where(order => order.Id == x.OrderId).Select(order => order.Number).First())
            .Select(x => new { x.Id, x.Status })
            .ToListAsync(cancellationToken);
        if (deliveries.Count == 0) return null;

        var now = clock.GetUtcNow();
        var route = DeliveryRoute.Create(driverUserId, StartOfLocalDay(DateOnly.FromDateTime(now.ToLocalTime().DateTime)), deliveries.Select(x => x.Id), driverUserId, now);
        if (deliveries.Any(x => x.Status == DeliveryStatus.InRoute)) route.ResumeFromAssignedDeliveries(now);
        db.DeliveryRoutes.Add(route);
        AuditRoute("DeliveryRoutePreparedForDriver", route.Id, driverUserId, now);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return await GetRouteAsync(route.Id, cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return await GetCurrentRouteAsync(driverUserId, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<DeliveryRouteListItem>> ListRoutesAsync(DateOnly promisedDate, Guid? driverUserId = null, CancellationToken cancellationToken = default)
    {
        var start = StartOfLocalDay(promisedDate);
        var end = start.AddDays(1);
        return await db.DeliveryRoutes.AsNoTracking()
            .Where(x => x.PromisedDateUtc >= start && x.PromisedDateUtc < end &&
                (driverUserId == null || x.DriverUserId == driverUserId) &&
                x.Stops.Any(stop => db.Deliveries.Any(delivery => delivery.Id == stop.DeliveryId &&
                    db.Orders.Any(order => order.Id == delivery.OrderId && order.ArchivedAtUtc == null))))
            .OrderByDescending(x => x.StartedAtUtc ?? x.CreatedAtUtc)
            .Select(x => new DeliveryRouteListItem(
                x.Id,
                x.DriverUserId,
                db.Users.Where(user => user.Id == x.DriverUserId).Select(user => user.DisplayName).FirstOrDefault() ?? "Repartidor",
                x.Status.ToString(),
                x.Stops.Count(stop => db.Deliveries.Any(delivery => delivery.Id == stop.DeliveryId &&
                    db.Orders.Any(order => order.Id == delivery.OrderId && order.ArchivedAtUtc == null))),
                x.CreatedAtUtc,
                x.StartedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeliveryItem>> ListAsync(Guid? assignedUserId = null, CancellationToken cancellationToken = default)
    {
        var items = await db.Deliveries.AsNoTracking().Where(x =>
                (assignedUserId == null || x.DriverUserId == assignedUserId) &&
                x.Status != DeliveryStatus.Cancelled &&
                db.Orders.Any(order => order.Id == x.OrderId && order.ArchivedAtUtc == null && order.Status != OrderStatus.Cancelled))
            .OrderBy(x => db.DeliveryRouteStops.Where(stop => stop.DeliveryId == x.Id).Select(stop => (int?)stop.Position).FirstOrDefault() ?? int.MaxValue).ThenBy(x => x.ScheduledAtUtc)
            .Select(x => new
            {
                x.Id, x.OrderId, Number=db.Orders.Where(o=>o.Id==x.OrderId).Select(o=>o.Number).First(),
                Customer=db.Customers.Where(c=>c.Id==db.Orders.Where(o=>o.Id==x.OrderId).Select(o=>o.CustomerId).First()).Select(c=>c.Name).First(),
                Status=x.Status.ToString(), x.DriverUserId,
                DriverName=x.DriverUserId==null?null:db.Users.Where(u=>u.Id==x.DriverUserId).Select(u=>u.DisplayName).FirstOrDefault(),
                x.ScheduledAtUtc, Pending=x.Lines.Sum(l=>l.AssignedQuantity-l.DeliveredQuantity), Delivered=x.Lines.Sum(l=>l.DeliveredQuantity), x.Version,
                DeliveryPointId=db.Orders.Where(o=>o.Id==x.OrderId).Select(o=>o.DeliveryPointId).FirstOrDefault(),
                DeliveryPoint=db.DeliveryPoints.Where(p=>p.Id==db.Orders.Where(o=>o.Id==x.OrderId).Select(o=>o.DeliveryPointId).FirstOrDefault()).Select(p=>p.Label).FirstOrDefault(),
                Address=db.DeliveryPoints.Where(p=>p.Id==db.Orders.Where(o=>o.Id==x.OrderId).Select(o=>o.DeliveryPointId).FirstOrDefault()).Select(p=>p.Address).FirstOrDefault(),
                Reference=db.DeliveryPoints.Where(p=>p.Id==db.Orders.Where(o=>o.Id==x.OrderId).Select(o=>o.DeliveryPointId).FirstOrDefault()).Select(p=>p.Reference).FirstOrDefault(),
                Location=db.DeliveryPoints.Where(p=>p.Id==db.Orders.Where(o=>o.Id==x.OrderId).Select(o=>o.DeliveryPointId).FirstOrDefault()).Select(p=>p.Location).FirstOrDefault(),
                RoutePosition=db.DeliveryRouteStops.Where(stop=>stop.DeliveryId==x.Id).Select(stop=>(int?)stop.Position).FirstOrDefault(),
                Total=db.OrderLines.Where(line=>line.OrderId==x.OrderId&&line.IsActive).Sum(line=>(decimal?)(line.SaleQuantity*line.SoldUnitPrice))??0,
                Paid=db.PaymentAllocations.Where(allocation=>allocation.OrderId==x.OrderId&&db.Payments.Any(payment=>payment.Id==allocation.PaymentId&&payment.Status==PaymentStatus.Confirmed)).Sum(allocation=>(decimal?)allocation.Amount)??0,
                DeliveryEvidenceCount=db.DeliveryEvidence.Count(evidence=>evidence.DeliveryId==x.Id&&evidence.IsActive),
                PaymentEvidenceCount=db.PaymentEvidence.Count(evidence=>db.Payments.Any(payment=>payment.Id==evidence.PaymentId&&payment.Status==PaymentStatus.Confirmed)&&db.PaymentAllocations.Any(allocation=>allocation.PaymentId==evidence.PaymentId&&allocation.OrderId==x.OrderId)),
                PaymentMethod=db.PaymentAllocations.Where(allocation=>allocation.OrderId==x.OrderId).Join(db.Payments.Where(payment=>payment.Status==PaymentStatus.Confirmed),allocation=>allocation.PaymentId,payment=>payment.Id,(allocation,payment)=>payment).OrderByDescending(payment=>payment.PaymentDateUtc).Select(payment=>payment.Method.ToString()).FirstOrDefault()
            }).ToListAsync(cancellationToken);
        var coordinateTasks = items.ToDictionary(x => x.Id, x => coordinateResolver.ResolveAsync(x.Location, x.Address, x.Reference, cancellationToken));
        await Task.WhenAll(coordinateTasks.Values);
        return items.Select(x =>
        {
            var coordinate = coordinateTasks[x.Id].Result;
            return new DeliveryItem(x.Id,x.Number,x.Customer,x.Status,x.DriverUserId,x.DriverName,x.ScheduledAtUtc,x.Pending,x.Version,x.RoutePosition,x.Total,x.Paid,Math.Max(0,x.Total-x.Paid),x.DeliveryEvidenceCount,x.PaymentEvidenceCount,x.PaymentMethod,x.Delivered,x.DeliveryPoint,x.DeliveryPointId,x.Address,x.Reference,x.Location,coordinate?.Latitude,coordinate?.Longitude);
        }).ToList();
    }

    public async Task<DeliveryDetail?> GetAsync(Guid deliveryId, Guid? assignedUserId = null, CancellationToken cancellationToken = default)
    {
        var header = await db.Deliveries.AsNoTracking()
            .Where(delivery => delivery.Id == deliveryId && (assignedUserId == null || delivery.DriverUserId == assignedUserId)
                && db.Orders.Any(order => order.Id == delivery.OrderId && order.ArchivedAtUtc == null))
            .Select(delivery => new
            {
                delivery.Id,
                delivery.OrderId,
                delivery.DriverUserId,
                delivery.ScheduledAtUtc,
                delivery.Version,
                Status = delivery.Status.ToString(),
                OrderNumber = db.Orders.Where(order => order.Id == delivery.OrderId).Select(order => order.Number).Single(),
                CustomerName = db.Orders.Where(order => order.Id == delivery.OrderId)
                    .Select(order => db.Customers.Where(customer => customer.Id == order.CustomerId).Select(customer => customer.Name).Single()).Single(),
                Address = db.Orders.Where(order => order.Id == delivery.OrderId)
                    .Select(order => db.DeliveryPoints.Where(point => point.Id == order.DeliveryPointId).Select(point => point.Address).SingleOrDefault()).Single(),
                Location = db.Orders.Where(order => order.Id == delivery.OrderId)
                    .Select(order => db.DeliveryPoints.Where(point => point.Id == order.DeliveryPointId).Select(point => point.Location).SingleOrDefault()).Single(),
                ContactName = db.Orders.Where(order => order.Id == delivery.OrderId)
                    .Select(order => db.CustomerContacts.Where(contact => contact.Id == order.ContactId).Select(contact => contact.Name).SingleOrDefault()).Single(),
                ContactPhone = db.Orders.Where(order => order.Id == delivery.OrderId)
                    .Select(order => db.CustomerContacts.Where(contact => contact.Id == order.ContactId).Select(contact => contact.Phone).SingleOrDefault()).Single(),
                Notes = db.Orders.Where(order => order.Id == delivery.OrderId).Select(order => order.Notes).Single(),
                DriverName = db.Users.Where(user => user.Id == delivery.DriverUserId).Select(user => user.DisplayName).SingleOrDefault(),
                Total = db.OrderLines.Where(line => line.OrderId == delivery.OrderId && line.IsActive)
                    .Sum(line => (decimal?)(line.SaleQuantity * line.SoldUnitPrice)) ?? 0,
                ReceivableVersion = db.Receivables.Where(receivable => receivable.OrderId == delivery.OrderId)
                    .Select(receivable => (int?)receivable.Version).SingleOrDefault() ?? 1
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (header is null) return null;

        var lines = await (from deliveryLine in db.DeliveryLines.AsNoTracking()
                           join orderLine in db.OrderLines.AsNoTracking() on deliveryLine.OrderLineId equals orderLine.Id
                           where deliveryLine.DeliveryId == header.Id
                           select new DeliveryLineItem(deliveryLine.Id, deliveryLine.OrderLineId, orderLine.ProductName,
                               deliveryLine.AssignedSaleQuantity, deliveryLine.AssignedReplacementQuantity, deliveryLine.AssignedTastingQuantity,
                               deliveryLine.AssignedQuantity, deliveryLine.DeliveredSaleQuantity, deliveryLine.DeliveredReplacementQuantity,
                               deliveryLine.DeliveredTastingQuantity, deliveryLine.DeliveredQuantity, deliveryLine.PendingQuantity,
                               deliveryLine.PendingSaleQuantity, deliveryLine.PendingReplacementQuantity, deliveryLine.PendingTastingQuantity))
            .ToListAsync(cancellationToken);
        var evidence = await db.DeliveryEvidence.AsNoTracking().Where(x => x.DeliveryId == header.Id && x.IsActive).OrderByDescending(x => x.UploadedAtUtc).Select(x => new EvidenceItem(x.Id, x.FileName, x.UploadedAtUtc)).ToListAsync(cancellationToken);
        var history = await db.DeliveryStatusHistory.AsNoTracking().Where(x => x.DeliveryId == header.Id).OrderByDescending(x => x.ChangedAtUtc).Select(x => new DeliveryHistoryItem(x.ChangedAtUtc, x.NewStatus.ToString(), x.Reason)).ToListAsync(cancellationToken);
        var paymentItems = await PaymentsAsync(header.OrderId, cancellationToken);
        var payments = paymentItems.Sum(x => x.Amount);
        return new DeliveryDetail(header.Id, header.OrderId, header.OrderNumber, header.CustomerName, header.Address, header.Location,
            header.ContactName, header.ContactPhone, header.Notes, header.Status, header.DriverUserId, header.DriverName,
            header.ScheduledAtUtc, header.Version, lines, evidence, history, Math.Max(0, header.Total - payments),
            header.ReceivableVersion, header.Total, payments, paymentItems);
    }

    public async Task<IReadOnlyList<DeliveryOrderOption>> SchedulableOrdersAsync(CancellationToken cancellationToken = default)
    {
        var orders = await db.Orders.AsNoTracking().Include(x => x.Lines).Include(x => x.Reservations).Where(x => x.ArchivedAtUtc == null && (x.Status == OrderStatus.Confirmed || x.Status == OrderStatus.InPreparation || x.Status == OrderStatus.Ready || x.Status == OrderStatus.OutForDelivery)).OrderBy(x => x.PromisedAtUtc).ToListAsync(cancellationToken);
        var active = new[] { DeliveryStatus.Scheduled, DeliveryStatus.Assigned, DeliveryStatus.InRoute, DeliveryStatus.Partial };
        var scheduled = await db.DeliveryLines.AsNoTracking().Where(x => active.Contains(db.Deliveries.Where(d => d.Id == x.DeliveryId).Select(d => d.Status).First())).GroupBy(x => x.OrderLineId).Select(x => new { Id = x.Key, Sale = x.Sum(y => y.AssignedSaleQuantity - y.DeliveredSaleQuantity), Replacement = x.Sum(y => y.AssignedReplacementQuantity - y.DeliveredReplacementQuantity), Tasting=x.Sum(y=>y.AssignedTastingQuantity-y.DeliveredTastingQuantity) }).ToDictionaryAsync(x => x.Id, cancellationToken);
        var delivered = await db.DeliveryLines.AsNoTracking().GroupBy(x=>x.OrderLineId).Select(x=>new{Id=x.Key,Sale=x.Sum(y=>y.DeliveredSaleQuantity),Replacement=x.Sum(y=>y.DeliveredReplacementQuantity),Tasting=x.Sum(y=>y.DeliveredTastingQuantity)}).ToDictionaryAsync(x=>x.Id,cancellationToken);
        return orders.Select(order => new DeliveryOrderOption(order.Id, order.Number, db.Customers.Where(x => x.Id == order.CustomerId).Select(x => x.Name).First(), order.PromisedAtUtc, order.Lines.Where(x => x.IsActive).Select(line => { scheduled.TryGetValue(line.Id,out var s); delivered.TryGetValue(line.Id,out var d); var sale=Math.Max(0,line.SaleQuantity-(s?.Sale??0)-(d?.Sale??0)); var replacement=Math.Max(0,line.ReplacementQuantity-(s?.Replacement??0)-(d?.Replacement??0)); var tasting=Math.Max(0,line.TastingQuantity-(s?.Tasting??0)-(d?.Tasting??0)); return new DeliveryOrderLineOption(line.Id,line.ProductName,sale,replacement,tasting,sale+replacement+tasting); }).Where(x => x.AvailableToSchedule > 0).ToList())).Where(x => x.Lines.Count > 0).ToList();
    }

    public async Task<IReadOnlyList<DriverOption>> DriverOptionsAsync(Guid? includeCurrentUserId = null, CancellationToken cancellationToken = default) =>
        await db.Users.AsNoTracking()
            .Where(x => x.IsEnabled &&
                (x.Id == includeCurrentUserId ||
                 db.UserRoles.Any(r => r.UserId == x.Id && db.Roles.Any(role => role.Id == r.RoleId && (role.Name == AppRoles.Delivery || db.RoleClaims.Any(c => c.RoleId == role.Id && c.ClaimType == AppPermissions.ClaimType && c.ClaimValue == AppPermissions.Deliveries))))))
            .OrderBy(x => x.DisplayName)
            .Select(x => new DriverOption(x.Id, x.DisplayName))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ReceivableItem>> ReceivablesAsync(CancellationToken cancellationToken = default) =>
        await db.Receivables.AsNoTracking().Where(x => db.Orders.Any(o => o.Id == x.OrderId && o.ArchivedAtUtc == null)).OrderByDescending(x => x.InvoicedAmount - x.PaidAmount).Select(x => new ReceivableItem(x.OrderId, db.Orders.Where(o => o.Id == x.OrderId).Select(o => o.Number).First(), db.Customers.Where(c => c.Id == x.CustomerId).Select(c => c.Name).First(), x.PayerId == null ? "Cliente" : db.PaymentResponsibleParties.Where(p => p.Id == x.PayerId).Select(p => p.Name).First(), x.InvoicedAmount, x.PaidAmount, x.InvoicedAmount > x.PaidAmount ? x.InvoicedAmount - x.PaidAmount : 0, x.Version, db.Deliveries.Where(d => d.OrderId == x.OrderId && d.CompletedAtUtc != null).Max(d => (DateTimeOffset?)d.CompletedAtUtc))).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PaymentHistoryItem>> PaymentsAsync(Guid orderId,CancellationToken cancellationToken=default)=>
        await db.PaymentAllocations.AsNoTracking().Where(x=>x.OrderId==orderId)
            .Join(db.Payments.Where(x=>x.Status==PaymentStatus.Confirmed),allocation=>allocation.PaymentId,payment=>payment.Id,(allocation,payment)=>new{allocation,payment})
            .OrderByDescending(x=>x.payment.PaymentDateUtc).Select(x=>new PaymentHistoryItem(x.payment.Id,x.allocation.Amount,x.payment.Method.ToString(),x.payment.PaymentDateUtc,x.payment.Notes,db.PaymentEvidence.Where(e=>e.PaymentId==x.payment.Id).Select(e=>e.Id).FirstOrDefault())).ToListAsync(cancellationToken);

    public async Task<SignedEvidenceResult> GetPaymentEvidenceUrlAsync(Guid evidenceId,Guid userId,CancellationToken cancellationToken=default)
    {
        var exists=await db.PaymentEvidence.AsNoTracking().AnyAsync(x=>x.Id==evidenceId,cancellationToken);
        return exists?new(true,$"/media/payment-evidence/{evidenceId:N}",[]):new(false,null,["El respaldo no existe."]);
    }

    public async Task<EvidenceContentResult> GetPaymentEvidenceContentAsync(Guid evidenceId,Guid userId,CancellationToken cancellationToken=default)
    {
        var evidence=await db.PaymentEvidence.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==evidenceId,cancellationToken);
        if(evidence is null)return new(false,null,null,["El respaldo no existe."]);
        var downloaded=await CloudinaryPrivateMedia.DownloadAsync(cloudinaryOptions.Value,httpClientFactory,evidence.PublicId,evidence.Format,evidence.FileName,clock.GetUtcNow().AddMinutes(5),cancellationToken);
        if(downloaded is null)return new(false,null,null,["No se pudo recuperar el respaldo."]);
        Audit("PaymentEvidenceViewed",evidence.PaymentId,userId,clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return new(true,downloaded.Value.Content,downloaded.Value.ContentType,[]);
    }

    private Payment AddPayment(Guid orderId, Guid? deliveryId, Receivable receivable, decimal amount, string? methodText, string? reference, bool collectedByUser, Guid userId, DateTimeOffset now, bool allowAdvance = false,string? notes=null,DateTimeOffset? paymentDate=null)
    {
        if (!Enum.TryParse<PaymentMethod>(methodText, true, out var method)) throw new ArgumentException("Método de pago inválido.");
        receivable.ApplyPayment(amount, allowAdvance);
        if(method is not (PaymentMethod.Cash or PaymentMethod.Qr))throw new ArgumentException("El método de pago debe ser efectivo o QR.");
        var payment = Payment.Create(orderId, deliveryId, amount, method, userId, reference, now,notes,paymentDate,receivable.PayerId??receivable.CustomerId);
        db.Payments.Add(payment);
        db.PaymentAllocations.Add(PaymentAllocation.Create(payment.Id,orderId,amount,1,now));
        if (collectedByUser) db.SettlementObligations.Add(SettlementObligation.Create(payment.Id, userId, payment.Amount, now));
        return payment;
    }

    private static (int Sale, int Replacement, int Tasting) PendingFromReservations(Order order, Dictionary<Guid, (int Sale, int Replacement, int Tasting)> delivered)
    {
        var sale = 0;
        var replacement = 0;
        var tasting = 0;
        foreach (var line in order.Lines.Where(x => x.IsActive))
        {
            var reservation = order.Reservations.FirstOrDefault(x => x.OrderLineId == line.Id && x.IsActive);
            if (reservation is null || reservation.ReservedQuantity == 0) continue;
            delivered.TryGetValue(line.Id, out var done);
            var available = reservation.ReservedQuantity;
            var lineSale = Math.Min(available, Math.Max(0, line.SaleQuantity - done.Sale));
            available -= lineSale;
            var lineReplacement = Math.Min(available, Math.Max(0, line.ReplacementQuantity - done.Replacement));
            available -= lineReplacement;
            var lineTasting = Math.Min(available, Math.Max(0, line.TastingQuantity - done.Tasting));
            sale += lineSale;
            replacement += lineReplacement;
            tasting += lineTasting;
        }
        return (sale, replacement, tasting);
    }

    private static int DeliveryPriority(DeliveryStatus status) => status switch
    {
        DeliveryStatus.InRoute => 6,
        DeliveryStatus.Assigned => 5,
        DeliveryStatus.Scheduled => 4,
        DeliveryStatus.Partial => 3,
        DeliveryStatus.Failed => 2,
        _ => 0
    };

    private static string BoardStatus(DeliveryStatus status) => status switch
    {
        DeliveryStatus.InRoute => "InRoute",
        DeliveryStatus.Delivered => "Delivered",
        DeliveryStatus.Partial or DeliveryStatus.Failed => "Incident",
        _ => "Pending"
    };

    private static DateTimeOffset StartOfLocalDay(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)).ToUniversalTime();
    }

    private async Task CompleteRouteIfNeededAsync(Guid deliveryId, bool deliveryCompleted, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!deliveryCompleted) return;
        var routeId = await db.DeliveryRouteStops.Where(x => x.DeliveryId == deliveryId).Select(x => (Guid?)x.DeliveryRouteId).SingleOrDefaultAsync(cancellationToken);
        if (routeId is null) return;
        var route = await db.DeliveryRoutes.Include(x => x.Stops).SingleOrDefaultAsync(x => x.Id == routeId.Value, cancellationToken);
        if (route is null || route.Status != DeliveryRouteStatus.InRoute) return;
        var otherIds = route.Stops.Where(x => x.DeliveryId != deliveryId).Select(x => x.DeliveryId).ToList();
        var allOtherDelivered = otherIds.Count == 0 || await db.Deliveries.AsNoTracking().Where(x => otherIds.Contains(x.Id)).AllAsync(x => x.Status == DeliveryStatus.Delivered, cancellationToken);
        if (allOtherDelivered)
        {
            route.Complete(now);
            AuditRoute("DeliveryRouteCompleted", route.Id, userId, now);
        }
    }

    private async Task<bool> IsDriverAsync(Guid userId, CancellationToken cancellationToken) => await db.Users.AnyAsync(x => x.Id == userId && x.IsEnabled && db.UserRoles.Any(r => r.UserId == x.Id && db.Roles.Any(role => role.Id == r.RoleId && (role.Name == AppRoles.Delivery || db.RoleClaims.Any(c => c.RoleId == role.Id && c.ClaimType == AppPermissions.ClaimType && c.ClaimValue == AppPermissions.Deliveries)))), cancellationToken);
    private async Task<bool> CanBeAssignedAsync(Guid assignedUserId, Guid currentUserId, CancellationToken cancellationToken)
    {
        if (await IsDriverAsync(assignedUserId, cancellationToken)) return true;
        if (assignedUserId != currentUserId) return false;
        return await db.Users.AnyAsync(x => x.Id == currentUserId && x.IsEnabled &&
            db.UserRoles.Any(userRole => userRole.UserId == x.Id &&
                db.Roles.Any(role => role.Id == userRole.RoleId && (role.Name == AppRoles.Administrator || db.RoleClaims.Any(c => c.RoleId == role.Id && c.ClaimType == AppPermissions.ClaimType && c.ClaimValue == AppPermissions.Administration)))), cancellationToken);
    }
    private async Task<bool> CanCoordinateRoutesAsync(Guid userId, CancellationToken cancellationToken) => await db.UserRoles.AnyAsync(x => x.UserId == userId && db.Roles.Any(r => r.Id == x.RoleId && (r.Name == AppRoles.Administrator || r.Name == AppRoles.Sales || db.RoleClaims.Any(c => c.RoleId == r.Id && c.ClaimType == AppPermissions.ClaimType && c.ClaimValue == AppPermissions.Sales))), cancellationToken);
    private async Task<bool> CanOperateAsync(Delivery delivery, Guid userId, CancellationToken cancellationToken) => delivery.DriverUserId == userId || await db.UserRoles.AnyAsync(x => x.UserId == userId && db.Roles.Any(r => r.Id == x.RoleId && (r.Name == AppRoles.Administrator || db.RoleClaims.Any(c => c.RoleId == r.Id && c.ClaimType == AppPermissions.ClaimType && c.ClaimValue == AppPermissions.Administration))), cancellationToken);
    private Task<Delivery?> LoadAsync(Guid id, CancellationToken cancellationToken) => db.Deliveries.Include(x => x.Lines).Include(x => x.History).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    private Task<bool> FindIdempotentAsync(string operation, string key, CancellationToken cancellationToken) => db.IdempotencyRecords.AsNoTracking().AnyAsync(x => x.Operation == operation && x.Key == key, cancellationToken);
    private async Task<bool> WaitForIdempotentAsync(string operation, string key, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (await FindIdempotentAsync(operation, key, cancellationToken)) return true;
            await Task.Delay(100, cancellationToken);
        }
        return false;
    }
    private static bool ValidKey(string key) => !string.IsNullOrWhiteSpace(key) && key.Length <= 160;
    private static IdempotencyRecord Idempotency(string operation, string key, DateTimeOffset now) => new() { Id = Guid.NewGuid(), Operation = operation, Key = key, CreatedAtUtc = now, ExpiresAtUtc = now.AddDays(30) };
    private void Audit(string action, Guid id, Guid userId, DateTimeOffset now) => db.AuditEntries.Add(new AuditEntry { Id = Guid.NewGuid(), Action = action, EntityType = "Delivery", EntityId = id.ToString(), UserId = userId, OccurredAtUtc = now });
    private void AuditRoute(string action, Guid id, Guid userId, DateTimeOffset now) => db.AuditEntries.Add(new AuditEntry { Id = Guid.NewGuid(), Action = action, EntityType = "DeliveryRoute", EntityId = id.ToString(), UserId = userId, OccurredAtUtc = now });
    private static DeliveryResult Ok(Guid id) => new(true, id, []);
    private static DeliveryResult Fail(string error) => new(false, null, [error]);
    private static DeliveryResult Conflict() => Fail("La operación fue modificada por otra sesión.");
}
