namespace TentacionSana.Domain.Orders;

public enum OrderStatus { Draft, Confirmed, InPreparation, Ready, OutForDelivery, Delivered, Cancelled }
public enum OrderPaymentStatus { Pending, Partial, Paid, Refunded }

public sealed class Order
{
    private Order() { }
    public Guid Id { get; private set; }
    public long Number { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid? DeliveryPointId { get; private set; }
    public Guid? ContactId { get; private set; }
    public Guid? PaymentResponsiblePartyId { get; private set; }
    public Guid? SourceRequestId { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTimeOffset? ArchivedAtUtc { get; private set; }
    public Guid? ArchivedByUserId { get; private set; }
    public string? ArchiveReason { get; private set; }
    public OrderPaymentStatus PaymentStatus { get; private set; }
    public DateTimeOffset? PromisedAtUtc { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public int Version { get; private set; }
    public List<OrderLine> Lines { get; private set; } = [];
    public List<OrderStatusHistory> StatusHistory { get; private set; } = [];
    public List<OrderChangeHistory> ChangeHistory { get; private set; } = [];
    public List<PromisedDateHistory> PromisedDateHistory { get; private set; } = [];
    public List<OrderSnapshot> Snapshots { get; private set; } = [];
    public List<StockReservation> Reservations { get; private set; } = [];

    public static Order CreateDraft(Guid customerId, Guid? requestId, Guid userId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), CustomerId = customerId, SourceRequestId = requestId,
        Status = OrderStatus.Draft, PaymentStatus = OrderPaymentStatus.Pending, CreatedAtUtc = now,
        UpdatedAtUtc = now, CreatedByUserId = userId, UpdatedByUserId = userId, Version = 1
    };

    public OrderLine AddLine(Guid productId, string name, int saleQuantity, int replacementQuantity, int tastingQuantity, decimal standardPrice, decimal soldPrice, string? discountReason, string? replacementReason, string? replacementNotes, Guid userId, DateTimeOffset now)
        => AddLine(productId,name,saleQuantity,replacementQuantity,tastingQuantity,standardPrice,soldPrice,discountReason,replacementReason,replacementNotes,tastingQuantity>0?"Sin especificar":null,null,userId,now);

    public OrderLine AddLine(Guid productId, string name, int saleQuantity, int replacementQuantity, int tastingQuantity, decimal standardPrice, decimal soldPrice, string? discountReason, string? replacementReason, string? replacementNotes, string? tastingReason, string? tastingNotes, Guid userId, DateTimeOffset now)
    {
        if (Status != OrderStatus.Draft) throw new InvalidOperationException("Solo se pueden editar líneas directamente en un pedido borrador.");
        var line = OrderLine.Create(Id, productId, name, saleQuantity, replacementQuantity, tastingQuantity, standardPrice, soldPrice, discountReason, replacementReason, replacementNotes, tastingReason, tastingNotes);
        Lines.Add(line);
        ChangeHistory.Add(OrderChangeHistory.Create(Id, "LineAdded", null, $"{name}: venta {saleQuantity}, reposición {replacementQuantity}, degustación {tastingQuantity}, total {line.Quantity} x {soldPrice:0.00}", userId, "Edición del pedido pendiente.", now));
        Touch(userId, now);
        return line;
    }

    public OrderLine AddLine(Guid productId, string name, int quantity, decimal standardPrice, decimal soldPrice, string? discountReason, Guid userId, DateTimeOffset now) =>
        AddLine(productId, name, quantity, 0, 0, standardPrice, soldPrice, discountReason, null, null, userId, now);

    public void UpdateLine(Guid lineId, int saleQuantity, int replacementQuantity, int tastingQuantity, decimal standardPrice, decimal soldPrice, string? discountReason, string? replacementReason, string? replacementNotes, Guid userId, DateTimeOffset now)
    {
        EnsureDraft();
        var line = Lines.SingleOrDefault(x => x.Id == lineId) ?? throw new ArgumentException("La línea no existe.");
        var previous = $"{line.Quantity} x {line.SoldUnitPrice:0.00}";
        line.Update(saleQuantity, replacementQuantity, tastingQuantity, standardPrice, soldPrice, discountReason, replacementReason, replacementNotes);
        ChangeHistory.Add(OrderChangeHistory.Create(Id, "LineUpdated", previous, $"venta {saleQuantity}, reposición {replacementQuantity}, degustación {tastingQuantity}, total {line.Quantity} x {soldPrice:0.00}", userId, "Edición del pedido pendiente.", now));
        Touch(userId, now);
    }

    public void Configure(Guid customerId, Guid? deliveryPointId, Guid? contactId, Guid? payerId, DateTimeOffset? promisedAtUtc, string? notes, Guid userId, DateTimeOffset now)
    {
        EnsureDraft();
        if (promisedAtUtc != PromisedAtUtc) PromisedDateHistory.Add(Orders.PromisedDateHistory.Create(Id, PromisedAtUtc, promisedAtUtc, userId, "Edición del borrador.", now));
        if (customerId != CustomerId) ChangeHistory.Add(OrderChangeHistory.Create(Id, "CustomerId", CustomerId.ToString(), customerId.ToString(), userId, "Edición del borrador.", now));
        CustomerId = customerId; DeliveryPointId = deliveryPointId; ContactId = contactId; PaymentResponsiblePartyId = payerId;
        PromisedAtUtc = promisedAtUtc; Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(); Touch(userId, now);
    }

    public void RemoveLine(Guid lineId, Guid userId, DateTimeOffset now) { EnsureDraft(); var line = Lines.SingleOrDefault(x => x.Id == lineId) ?? throw new ArgumentException("La línea no existe."); Lines.Remove(line); ChangeHistory.Add(OrderChangeHistory.Create(Id, "LineRemoved", line.ProductName, null, userId, "Edición del borrador.", now)); Touch(userId, now); }

    public void Confirm(OrderSnapshot snapshot, Guid userId, DateTimeOffset now)
    {
        EnsureDraft();
        if (Lines.Count == 0) throw new InvalidOperationException("El pedido requiere al menos una línea.");
        if (PromisedAtUtc is null) throw new InvalidOperationException("La fecha prometida es obligatoria para confirmar.");
        Snapshots.Add(snapshot); Status = OrderStatus.Confirmed; StatusHistory.Add(OrderStatusHistory.Create(Id, OrderStatus.Draft, Status, userId, "Pedido confirmado.", now));
        foreach (var line in Lines) Reservations.Add(StockReservation.Create(Id, line.Id, line.ProductId, line.Quantity, now));
        Touch(userId, now);
    }

    public void Cancel(string reason, Guid userId, DateTimeOffset now)
    {
        if (Status is OrderStatus.Cancelled or OrderStatus.Delivered) throw new InvalidOperationException("El pedido no puede cancelarse en su estado actual.");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("El motivo de cancelación es obligatorio.");
        var previous = Status; Status = OrderStatus.Cancelled; foreach (var reservation in Reservations) reservation.Release(now);
        StatusHistory.Add(OrderStatusHistory.Create(Id, previous, Status, userId, reason, now)); Touch(userId, now);
    }

    public void Archive(string reason, Guid userId, DateTimeOffset now)
    {
        if (ArchivedAtUtc is not null) throw new InvalidOperationException("El pedido ya fue eliminado de la vista.");
        if (Status is not (OrderStatus.Delivered or OrderStatus.Cancelled)) throw new InvalidOperationException("Solo se pueden archivar pedidos entregados o cancelados.");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("El motivo de eliminación es obligatorio.");
        ArchivedAtUtc = now;
        ArchivedByUserId = userId;
        ArchiveReason = reason.Trim();
        ChangeHistory.Add(OrderChangeHistory.Create(Id, "Archived", null, now.ToString("O"), userId, ArchiveReason, now));
        Touch(userId, now);
    }

    public void DetachSourceRequest(Guid userId, DateTimeOffset now)
    {
        if (SourceRequestId is not { } requestId) return;
        SourceRequestId = null;
        ChangeHistory.Add(OrderChangeHistory.Create(Id, "SourceRequestDetached", requestId.ToString(), null, userId, "La solicitud se reabrió tras eliminar el pedido pendiente.", now));
        Touch(userId, now);
    }

    public void ReviseConfiguration(Guid customerId, Guid? deliveryPointId, Guid? contactId, Guid? payerId, DateTimeOffset? promisedAtUtc, string? notes, string reason, Guid userId, DateTimeOffset now)
    {
        EnsureModifiable(reason);
        RecordChange("CustomerId", CustomerId.ToString(), customerId.ToString(), reason, userId, now);
        RecordChange("DeliveryPointId", DeliveryPointId?.ToString(), deliveryPointId?.ToString(), reason, userId, now);
        RecordChange("ContactId", ContactId?.ToString(), contactId?.ToString(), reason, userId, now);
        RecordChange("PaymentResponsiblePartyId", PaymentResponsiblePartyId?.ToString(), payerId?.ToString(), reason, userId, now);
        RecordChange("Notes", Notes, string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(), reason, userId, now);
        if (PromisedAtUtc != promisedAtUtc) PromisedDateHistory.Add(Orders.PromisedDateHistory.Create(Id, PromisedAtUtc, promisedAtUtc, userId, reason, now));
        CustomerId=customerId; DeliveryPointId=deliveryPointId; ContactId=contactId; PaymentResponsiblePartyId=payerId; PromisedAtUtc=promisedAtUtc; Notes=string.IsNullOrWhiteSpace(notes)?null:notes.Trim(); Touch(userId,now);
    }

    public void ReviseLine(Guid lineId, int saleQuantity, int replacementQuantity, int tastingQuantity, decimal standardPrice, decimal soldPrice, string? discountReason, string? replacementReason, string? replacementNotes, string? tastingReason, string? tastingNotes, string reason, Guid userId, DateTimeOffset now)
    {
        EnsureModifiable(reason);
        var line=Lines.SingleOrDefault(x=>x.Id==lineId)??throw new ArgumentException("La línea no existe.");
        var previous=$"{line.Quantity} x {line.SoldUnitPrice:0.00}"; line.Update(saleQuantity,replacementQuantity,tastingQuantity,standardPrice,soldPrice,discountReason,replacementReason,replacementNotes,tastingReason,tastingNotes);
        var reservation=Reservations.SingleOrDefault(x=>x.OrderLineId==lineId&&x.IsActive)??throw new InvalidOperationException("La línea confirmada no tiene una reserva activa.");
        reservation.Adjust(line.Quantity,now); ChangeHistory.Add(OrderChangeHistory.Create(Id,"LineUpdated",previous,$"venta {saleQuantity}, reposición {replacementQuantity}, degustación {tastingQuantity}, total {line.Quantity} x {soldPrice:0.00}",userId,reason,now)); Touch(userId,now);
    }

    public void ReviseLine(Guid lineId, int quantity, decimal standardPrice, decimal soldPrice, string? discountReason, string reason, Guid userId, DateTimeOffset now) =>
        ReviseLine(lineId, quantity, 0, 0, standardPrice, soldPrice, discountReason, null, null, null, null, reason, userId, now);

    public void RemoveConfirmedLine(Guid lineId,string reason,Guid userId,DateTimeOffset now)
    {
        EnsureModifiable(reason);var line=Lines.SingleOrDefault(x=>x.Id==lineId&&x.IsActive)??throw new ArgumentException("La línea no existe o ya fue retirada.");line.Deactivate();var reservation=Reservations.SingleOrDefault(x=>x.OrderLineId==lineId&&x.IsActive);reservation?.Release(now);ChangeHistory.Add(OrderChangeHistory.Create(Id,"LineRemoved",$"{line.ProductName}: {line.Quantity} x {line.SoldUnitPrice:0.00}",null,userId,reason,now));Touch(userId,now);
    }

    public void AdvanceTo(OrderStatus next, string reason, Guid userId, DateTimeOffset now)
    {
        if(string.IsNullOrWhiteSpace(reason))throw new ArgumentException("El motivo del cambio de estado es obligatorio.");
        var valid=(Status,next) switch { (OrderStatus.Confirmed,OrderStatus.InPreparation)=>true,(OrderStatus.InPreparation,OrderStatus.Ready)=>true,_=>false };
        if(!valid)throw new InvalidOperationException("La transición de estado no está permitida."); var previous=Status;Status=next;StatusHistory.Add(OrderStatusHistory.Create(Id,previous,next,userId,reason,now));Touch(userId,now);
    }

    public void MarkOutForDelivery(Guid userId,DateTimeOffset now)=>TransitionFromDelivery(OrderStatus.Ready,OrderStatus.OutForDelivery,"Entrega iniciada.",userId,now);
    public void ReturnToReady(Guid userId,DateTimeOffset now)=>TransitionFromDelivery(OrderStatus.OutForDelivery,OrderStatus.Ready,"Salida de ruta revertida.",userId,now);
    public void MarkDelivered(Guid userId,DateTimeOffset now)=>TransitionFromDelivery(OrderStatus.OutForDelivery,OrderStatus.Delivered,"Todas las unidades fueron entregadas.",userId,now);
    private void TransitionFromDelivery(OrderStatus expected,OrderStatus next,string reason,Guid userId,DateTimeOffset now){if(Status!=expected)throw new InvalidOperationException("El estado del pedido no permite esta operación de entrega.");var previous=Status;Status=next;StatusHistory.Add(OrderStatusHistory.Create(Id,previous,next,userId,reason,now));Touch(userId,now);}

    private void EnsureModifiable(string reason){if(Status is OrderStatus.Draft or OrderStatus.Delivered or OrderStatus.Cancelled)throw new InvalidOperationException("El pedido no admite esta modificación en su estado actual.");if(string.IsNullOrWhiteSpace(reason))throw new ArgumentException("El motivo de la modificación es obligatorio.");}
    private void RecordChange(string field,string? old,string? current,string reason,Guid user,DateTimeOffset now){if(old!=current)ChangeHistory.Add(OrderChangeHistory.Create(Id,field,old,current,user,reason,now));}

    private void EnsureDraft() { if (Status != OrderStatus.Draft) throw new InvalidOperationException("El pedido ya no está en borrador."); }
    private void Touch(Guid userId, DateTimeOffset now) { UpdatedByUserId = userId; UpdatedAtUtc = now; Version++; }
}

public sealed class OrderLine
{
    private OrderLine() { }
    public Guid Id { get; private set; } public Guid OrderId { get; private set; } public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = ""; public int Quantity { get; private set; }
    public int SaleQuantity { get; private set; }
    public int ReplacementQuantity { get; private set; }
    public int TastingQuantity { get; private set; }
    public string? ReplacementReason { get; private set; }
    public string? ReplacementNotes { get; private set; }
    public string? TastingReason { get; private set; }
    public string? TastingNotes { get; private set; }
    public decimal StandardUnitPrice { get; private set; } public decimal SoldUnitPrice { get; private set; }
    public decimal UnitDiscount { get; private set; } public string? DiscountReason { get; private set; }
    public bool IsActive { get; private set; }
    public decimal LineTotal => decimal.Round(SoldUnitPrice * SaleQuantity, 2, MidpointRounding.AwayFromZero);
    internal static OrderLine Create(Guid orderId, Guid productId, string name, int saleQuantity, int replacementQuantity, int tastingQuantity, decimal standard, decimal sold, string? reason, string? replacementReason, string? replacementNotes, string? tastingReason = null, string? tastingNotes = null)
    {
        if (saleQuantity < 0 || replacementQuantity < 0 || tastingQuantity < 0 || saleQuantity + replacementQuantity + tastingQuantity is < 1 or > 10000) throw new ArgumentException("El total físico debe estar entre 1 y 10000 unidades.");
        if (replacementQuantity > 0 && string.IsNullOrWhiteSpace(replacementReason)) throw new ArgumentException("El motivo de reposición es obligatorio.");
        if (tastingQuantity > 0 && string.IsNullOrWhiteSpace(tastingReason)) throw new ArgumentException("El motivo o destino de degustación es obligatorio.");
        if (standard < 0 || sold < 0) throw new ArgumentException("Los precios no pueden ser negativos.");
        standard = decimal.Round(standard, 2, MidpointRounding.AwayFromZero); sold = decimal.Round(sold, 2, MidpointRounding.AwayFromZero);
        if (sold < standard && string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Todo descuento exige un motivo.");
        return new() { Id=Guid.NewGuid(),OrderId=orderId,ProductId=productId,ProductName=name.Trim(),Quantity=saleQuantity+replacementQuantity+tastingQuantity,SaleQuantity=saleQuantity,ReplacementQuantity=replacementQuantity,TastingQuantity=tastingQuantity,ReplacementReason=string.IsNullOrWhiteSpace(replacementReason)?null:replacementReason.Trim(),ReplacementNotes=string.IsNullOrWhiteSpace(replacementNotes)?null:replacementNotes.Trim(),TastingReason=string.IsNullOrWhiteSpace(tastingReason)?null:tastingReason.Trim(),TastingNotes=string.IsNullOrWhiteSpace(tastingNotes)?null:tastingNotes.Trim(),StandardUnitPrice=standard,SoldUnitPrice=sold,UnitDiscount=Math.Max(0,standard-sold),DiscountReason=string.IsNullOrWhiteSpace(reason)?null:reason.Trim(),IsActive=true };
    }
    internal void Update(int saleQuantity, int replacementQuantity, int tastingQuantity, decimal standard, decimal sold, string? reason, string? replacementReason, string? replacementNotes, string? tastingReason = null, string? tastingNotes = null)
    {
        var updated = Create(OrderId, ProductId, ProductName, saleQuantity, replacementQuantity, tastingQuantity, standard, sold, reason, replacementReason, replacementNotes, tastingReason, tastingNotes);
        Quantity = updated.Quantity; SaleQuantity=updated.SaleQuantity; ReplacementQuantity=updated.ReplacementQuantity; TastingQuantity=updated.TastingQuantity; ReplacementReason=updated.ReplacementReason; ReplacementNotes=updated.ReplacementNotes; StandardUnitPrice = updated.StandardUnitPrice; SoldUnitPrice = updated.SoldUnitPrice;
        TastingReason=updated.TastingReason; TastingNotes=updated.TastingNotes; UnitDiscount = updated.UnitDiscount; DiscountReason = updated.DiscountReason;
    }
    internal void Deactivate()=>IsActive=false;
}

public sealed class OrderStatusHistory { private OrderStatusHistory(){} public Guid Id{get;private set;} public Guid OrderId{get;private set;} public OrderStatus PreviousStatus{get;private set;} public OrderStatus NewStatus{get;private set;} public Guid ChangedByUserId{get;private set;} public string Reason{get;private set;}=""; public DateTimeOffset ChangedAtUtc{get;private set;} internal static OrderStatusHistory Create(Guid id,OrderStatus old,OrderStatus current,Guid user,string reason,DateTimeOffset now)=>new(){Id=Guid.NewGuid(),OrderId=id,PreviousStatus=old,NewStatus=current,ChangedByUserId=user,Reason=reason,ChangedAtUtc=now}; }
public sealed class OrderChangeHistory { private OrderChangeHistory(){} public Guid Id{get;private set;} public Guid OrderId{get;private set;} public string Field{get;private set;}=""; public string? PreviousValue{get;private set;} public string? NewValue{get;private set;} public Guid ChangedByUserId{get;private set;} public string Reason{get;private set;}=""; public DateTimeOffset ChangedAtUtc{get;private set;} internal static OrderChangeHistory Create(Guid id,string field,string? old,string? current,Guid user,string reason,DateTimeOffset now)=>new(){Id=Guid.NewGuid(),OrderId=id,Field=field,PreviousValue=old,NewValue=current,ChangedByUserId=user,Reason=reason,ChangedAtUtc=now}; }
public sealed class PromisedDateHistory { private PromisedDateHistory(){} public Guid Id{get;private set;} public Guid OrderId{get;private set;} public DateTimeOffset? PreviousDateUtc{get;private set;} public DateTimeOffset? NewDateUtc{get;private set;} public Guid ChangedByUserId{get;private set;} public string Reason{get;private set;}=""; public DateTimeOffset ChangedAtUtc{get;private set;} internal static PromisedDateHistory Create(Guid id,DateTimeOffset? old,DateTimeOffset? current,Guid user,string reason,DateTimeOffset now)=>new(){Id=Guid.NewGuid(),OrderId=id,PreviousDateUtc=old,NewDateUtc=current,ChangedByUserId=user,Reason=reason,ChangedAtUtc=now}; }
public sealed class OrderSnapshot { private OrderSnapshot(){} public Guid Id{get;private set;} public Guid OrderId{get;private set;} public string CustomerName{get;private set;}=""; public string? DeliveryPointLabel{get;private set;} public string? DeliveryAddress{get;private set;} public string? DeliveryReference{get;private set;} public string? DeliveryLocation{get;private set;} public string? ContactName{get;private set;} public string? ContactPhone{get;private set;} public string? PayerName{get;private set;} public string? LinesJson{get;private set;} public DateTimeOffset CreatedAtUtc{get;private set;} public static OrderSnapshot Create(Guid orderId,string customer,string? point,string? address,string? contact,string? phone,string? payer,DateTimeOffset now,string? reference=null,string? location=null,string? linesJson=null)=>new(){Id=Guid.NewGuid(),OrderId=orderId,CustomerName=customer,DeliveryPointLabel=point,DeliveryAddress=address,DeliveryReference=reference,DeliveryLocation=location,ContactName=contact,ContactPhone=phone,PayerName=payer,LinesJson=linesJson,CreatedAtUtc=now}; }
public sealed class StockReservation { private StockReservation(){} public Guid Id{get;private set;} public Guid OrderId{get;private set;} public Guid OrderLineId{get;private set;} public Guid ProductId{get;private set;} public int Quantity{get;private set;} public int ReservedQuantity{get;private set;} public int ShortageQuantity{get;private set;} public bool IsActive{get;private set;} public DateTimeOffset CreatedAtUtc{get;private set;} public DateTimeOffset? ReleasedAtUtc{get;private set;} internal static StockReservation Create(Guid order,Guid line,Guid product,int quantity,DateTimeOffset now)=>new(){Id=Guid.NewGuid(),OrderId=order,OrderLineId=line,ProductId=product,Quantity=quantity,ShortageQuantity=quantity,IsActive=true,CreatedAtUtc=now}; public void Allocate(int reserved){if(reserved<0||reserved>Quantity)throw new ArgumentException("La asignación de reserva no es válida.");ReservedQuantity=reserved;ShortageQuantity=Quantity-reserved;} public void Consume(int quantity){if(quantity<1||quantity>ReservedQuantity)throw new InvalidOperationException("La entrega supera las unidades reservadas.");Quantity-=quantity;ReservedQuantity-=quantity;ShortageQuantity=Math.Max(0,Quantity-ReservedQuantity);if(Quantity==0)IsActive=false;} internal void Adjust(int quantity,DateTimeOffset now){if(quantity<1)throw new ArgumentException("Una línea confirmada debe conservar al menos una unidad.");Quantity=quantity;ReservedQuantity=Math.Min(ReservedQuantity,quantity);ShortageQuantity=quantity-ReservedQuantity;ReleasedAtUtc=null;IsActive=true;} internal void Release(DateTimeOffset now){IsActive=false;ReservedQuantity=0;ShortageQuantity=0;ReleasedAtUtc=now;} }
