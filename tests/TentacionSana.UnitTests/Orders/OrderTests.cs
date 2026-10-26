using TentacionSana.Domain.Orders;
using TentacionSana.Domain.Deliveries;

namespace TentacionSana.UnitTests.Orders;

public sealed class OrderTests
{
    [Fact]
    public void DeliveryFeeIsOptionalValidatedAndRecordedInHistory()
    {
        var order = Order.CreateDraft(Guid.NewGuid(), null, UserId, Now);

        Assert.Equal(0m, order.DeliveryFee);
        Assert.Throws<ArgumentException>(() => order.SetDeliveryFee(-1m, UserId, Now));
        Assert.Throws<ArgumentException>(() => order.SetDeliveryFee(12.345m, UserId, Now));

        order.SetDeliveryFee(15.50m, UserId, Now);

        Assert.Equal(15.50m, order.DeliveryFee);
        Assert.Equal("DeliveryFee", order.ChangeHistory[^1].Field);
        Assert.Equal("15.50", order.ChangeHistory[^1].NewValue);
    }

    [Fact]
    public void ReadyOrderCanBecomeDeliveredWhenAssignedDeliveryIsCompleted()
    {
        var order = Order.CreateDraft(Guid.NewGuid(), null, UserId, Now);
        order.Configure(order.CustomerId, null, null, null, Now.AddDays(1), null, UserId, Now);
        order.AddLine(Guid.NewGuid(), "Producto", 1, 20m, 20m, null, UserId, Now);
        order.Confirm(OrderSnapshot.Create(order.Id, "Cliente", null, null, null, null, null, Now), UserId, Now);
        order.AdvanceTo(OrderStatus.InPreparation, "Preparando", UserId, Now);
        order.AdvanceTo(OrderStatus.Ready, "Listo", UserId, Now);

        order.MarkDelivered(UserId, Now.AddMinutes(1));

        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal(OrderStatus.Ready, order.StatusHistory[^1].PreviousStatus);
    }

    [Fact]
    public void DeliveredOrderCorrectionReclassifiesSaleAndKeepsPhysicalQuantity()
    {
        var order = Order.CreateDraft(Guid.NewGuid(), null, UserId, Now);
        order.Configure(order.CustomerId, null, null, null, Now.AddDays(1), null, UserId, Now);
        var line = order.AddLine(Guid.NewGuid(), "Producto", 15, 0, 1, 20m, 15m, "Mayorista", null, null, "Muestra comercial", null, UserId, Now);
        order.Confirm(OrderSnapshot.Create(order.Id, "Cliente", null, null, null, null, null, Now), UserId, Now);
        order.AdvanceTo(OrderStatus.InPreparation, "Preparando", UserId, Now);
        order.AdvanceTo(OrderStatus.Ready, "Listo", UserId, Now);
        order.MarkDelivered(UserId, Now);

        order.CorrectDeliveredLine(line.Id, 14, 0, 2, 15m, "Mayorista", null, null, "Muestra comercial", null,
            "Una venta fue una muestra", UserId, Now.AddMinutes(1));

        Assert.Equal(16, line.Quantity);
        Assert.Equal(14, line.SaleQuantity);
        Assert.Equal(2, line.TastingQuantity);
        Assert.Equal(210m, line.LineTotal);
        Assert.Equal("DeliveredLineCorrected", order.ChangeHistory[^1].Field);
        order.CorrectDeliveredLine(line.Id, 18, 0, 1, 15m, "Mayorista", null, null, "Muestra comercial", null,
            "Tres ventas ya entregadas no estaban registradas", UserId, Now.AddMinutes(2));
        Assert.Equal(19, line.Quantity);
        Assert.Equal(270m, line.LineTotal);
        Assert.Throws<InvalidOperationException>(() => order.CorrectDeliveredLine(line.Id, 13, 0, 2, 15m,
            "Mayorista", null, null, "Muestra comercial", null, "Cantidad equivocada", UserId, Now));
    }

    [Fact]
    public void CorrectedInvoiceChangesBalanceWithoutChangingRegisteredPayment()
    {
        var receivable = Receivable.Create(Guid.NewGuid(), Guid.NewGuid(), null);
        receivable.AddSale(225m);
        receivable.ApplyPayment(100m);

        receivable.CorrectInvoice(210m);

        Assert.Equal(210m, receivable.InvoicedAmount);
        Assert.Equal(100m, receivable.PaidAmount);
        Assert.Equal(110m, receivable.Balance);
    }

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DeliveredOrderCanBeArchivedWithoutChangingItsStatusOrLines()
    {
        var order = Order.CreateDraft(Guid.NewGuid(), null, UserId, Now);
        order.Configure(order.CustomerId, null, null, null, Now.AddDays(1), null, UserId, Now);
        var line = order.AddLine(Guid.NewGuid(), "Producto", 1, 20m, 20m, null, UserId, Now);
        order.Confirm(OrderSnapshot.Create(order.Id, "Cliente", null, null, null, null, null, Now), UserId, Now);
        order.AdvanceTo(OrderStatus.InPreparation, "Preparando", UserId, Now);
        order.AdvanceTo(OrderStatus.Ready, "Listo", UserId, Now);
        order.MarkOutForDelivery(UserId, Now);
        order.MarkDelivered(UserId, Now);

        order.Archive("Registro duplicado", UserId, Now.AddMinutes(1));

        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal(Now.AddMinutes(1), order.ArchivedAtUtc);
        Assert.Equal(UserId, order.ArchivedByUserId);
        Assert.Equal("Registro duplicado", order.ArchiveReason);
        Assert.Contains(line, order.Lines);
        Assert.Throws<InvalidOperationException>(() => order.Archive("Otra vez", UserId, Now));
    }

    [Fact]
    public void ActiveOrderCannotBeArchived()
    {
        var order = Order.CreateDraft(Guid.NewGuid(), null, UserId, Now);
        Assert.Throws<InvalidOperationException>(() => order.Archive("Registro duplicado", UserId, Now));
        order.Cancel("Cliente desistió", UserId, Now);
        Assert.Throws<ArgumentException>(() => order.Archive(" ", UserId, Now));
        order.Archive("Registro duplicado", UserId, Now);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void DeletedDraftKeepsHistoryAndReleasesItsSourceRequest()
    {
        var requestId = Guid.NewGuid();
        var order = Order.CreateDraft(Guid.NewGuid(), requestId, UserId, Now);

        order.Cancel("Pedido pendiente eliminado.", UserId, Now);
        order.DetachSourceRequest(UserId, Now);
        order.Archive("Pedido pendiente eliminado.", UserId, Now);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Null(order.SourceRequestId);
        Assert.Equal(Now, order.ArchivedAtUtc);
        Assert.Contains(order.ChangeHistory, change => change.Field == "SourceRequestDetached");
    }

    [Fact]
    public void DiscountRequiresReasonAndFreezesBothPrices()
    {
        var order = Order.CreateDraft(Guid.NewGuid(), null, UserId, Now);
        Assert.Throws<ArgumentException>(() => order.AddLine(Guid.NewGuid(), "Budín", 2, 20m, 18m, null, UserId, Now));

        var line = order.AddLine(Guid.NewGuid(), "Budín", 2, 20m, 18m, "Promoción", UserId, Now);

        Assert.Equal(20m, line.StandardUnitPrice);
        Assert.Equal(18m, line.SoldUnitPrice);
        Assert.Equal(2m, line.UnitDiscount);
        Assert.Equal(36m, line.LineTotal);
    }

    [Fact]
    public void ConfirmCreatesSnapshotHistoryAndReservation()
    {
        var order = Order.CreateDraft(Guid.NewGuid(), null, UserId, Now);
        order.Configure(order.CustomerId, null, null, null, Now.AddDays(1), null, UserId, Now);
        order.AddLine(Guid.NewGuid(), "Budín", 3, 20m, 20m, null, UserId, Now);

        order.Confirm(OrderSnapshot.Create(order.Id, "Cliente", null, null, null, null, null, Now), UserId, Now);

        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Single(order.Snapshots);
        Assert.Single(order.StatusHistory);
        Assert.Equal(3, Assert.Single(order.Reservations).ShortageQuantity);
    }

    [Fact]
    public void CancelReleasesActiveReservationsAndRecordsReason()
    {
        var order = Order.CreateDraft(Guid.NewGuid(), null, UserId, Now);
        order.Configure(order.CustomerId, null, null, null, Now.AddDays(1), null, UserId, Now);
        order.AddLine(Guid.NewGuid(), "Budín", 1, 20m, 20m, null, UserId, Now);
        order.Confirm(OrderSnapshot.Create(order.Id, "Cliente", null, null, null, null, null, Now), UserId, Now);

        order.Cancel("Cliente desistió", UserId, Now.AddMinutes(1));

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.False(Assert.Single(order.Reservations).IsActive);
        Assert.Equal("Cliente desistió", order.StatusHistory[^1].Reason);
    }

    [Fact]
    public void ConfirmedLineRevisionRequiresReasonAndAdjustsReservation()
    {
        var order = Order.CreateDraft(Guid.NewGuid(), null, UserId, Now);
        order.Configure(order.CustomerId, null, null, null, Now.AddDays(1), null, UserId, Now);
        var line = order.AddLine(Guid.NewGuid(), "Budín", 4, 20m, 20m, null, UserId, Now);
        order.Confirm(OrderSnapshot.Create(order.Id, "Cliente", null, null, null, null, null, Now), UserId, Now);

        Assert.Throws<ArgumentException>(() => order.ReviseLine(line.Id, 2, 20m, 20m, null, "", UserId, Now));
        order.ReviseLine(line.Id, 2, 20m, 20m, null, "Reducir pedido", UserId, Now);

        Assert.Equal(2, line.Quantity);
        Assert.Equal(2, Assert.Single(order.Reservations).Quantity);
        Assert.Equal(2, Assert.Single(order.Reservations).ShortageQuantity);
    }

    [Fact]
    public void ConfirmedLineCanChangeTwentySalesToOneSaleAndOneTasting()
    {
        var order = Order.CreateDraft(Guid.NewGuid(), null, UserId, Now);
        order.Configure(order.CustomerId, null, null, null, Now.AddDays(1), null, UserId, Now);
        var line = order.AddLine(Guid.NewGuid(), "Abro", 20, 0, 0, 15m, 15m, null, null, null, null, null, UserId, Now);
        order.Confirm(OrderSnapshot.Create(order.Id, "Cliente", null, null, null, null, null, Now), UserId, Now);
        var reservation = Assert.Single(order.Reservations);
        reservation.Allocate(20);

        order.ReviseLine(line.Id, line.ProductId, line.ProductName, 1, 0, 1, 15m, 15m, null, null, null,
            "Muestra comercial", null, "Cliente pidió una muestra", UserId, Now.AddMinutes(1));

        Assert.Equal(1, line.SaleQuantity);
        Assert.Equal(1, line.TastingQuantity);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(15m, line.LineTotal);
        Assert.Equal(2, reservation.Quantity);
        Assert.Equal(2, reservation.ReservedQuantity);
        Assert.Equal(0, reservation.ShortageQuantity);
    }

    [Fact]
    public void OutForDeliveryCannotBeSelectedManually()
    {
        var order = Order.CreateDraft(Guid.NewGuid(), null, UserId, Now);
        order.Configure(order.CustomerId, null, null, null, Now.AddDays(1), null, UserId, Now);
        var line = order.AddLine(Guid.NewGuid(), "Budín", 1, 20m, 20m, null, UserId, Now);
        order.Confirm(OrderSnapshot.Create(order.Id, "Cliente", null, null, null, null, null, Now), UserId, Now);
        order.AdvanceTo(OrderStatus.InPreparation, "Preparar", UserId, Now);
        order.AdvanceTo(OrderStatus.Ready, "Listo", UserId, Now);
        Assert.Throws<InvalidOperationException>(() => order.AdvanceTo(OrderStatus.OutForDelivery, "Reparto", UserId, Now));
    }

    [Theory]
    [InlineData(10, 2, 15, 12, 150)]
    [InlineData(10, 0, 16, 10, 160)]
    [InlineData(0, 2, 15, 2, 0)]
    public void SaleAndReplacementHaveIndependentPhysicalAndFinancialTotals(int sale,int replacement,decimal price,int physical,decimal collectible)
    {
        var order=Order.CreateDraft(Guid.NewGuid(),null,UserId,Now);
        var line=order.AddLine(Guid.NewGuid(),"Budín",sale,replacement,0,price,price,null,replacement>0?"Producto defectuoso":null,null,UserId,Now);

        Assert.Equal(physical,line.Quantity);
        Assert.Equal(collectible,line.LineTotal);
    }

    [Fact]
    public void ReplacementRequiresReason()
    {
        var order=Order.CreateDraft(Guid.NewGuid(),null,UserId,Now);
        Assert.Throws<ArgumentException>(()=>order.AddLine(Guid.NewGuid(),"Budín",10,2,0,15,15,null,null,null,UserId,Now));
    }

    [Fact]
    public void TastingLeavesInventoryButDoesNotGenerateIncome()
    {
        var order=Order.CreateDraft(Guid.NewGuid(),null,UserId,Now);
        var line=order.AddLine(Guid.NewGuid(),"Budín",10,2,3,15,15,null,"Producto defectuoso",null,UserId,Now);

        Assert.Equal(15,line.Quantity);
        Assert.Equal(3,line.TastingQuantity);
        Assert.Equal(150,line.LineTotal);
    }

    [Fact]
    public void TastingRequiresReasonAndPreservesItsDestination()
    {
        var order=Order.CreateDraft(Guid.NewGuid(),null,UserId,Now);

        Assert.Throws<ArgumentException>(()=>order.AddLine(Guid.NewGuid(),"Budín",0,0,5,15,15,null,null,null,null,null,UserId,Now));

        var line=order.AddLine(Guid.NewGuid(),"Budín",0,0,5,15,15,null,null,null,"Muestra comercial","Presentación a nuevos clientes",UserId,Now);
        Assert.Equal(5,line.Quantity);
        Assert.Equal(0,line.LineTotal);
        Assert.Equal("Muestra comercial",line.TastingReason);
        Assert.Equal("Presentación a nuevos clientes",line.TastingNotes);
    }

    [Fact]
    public void SaleReplacementAndTastingUsePhysicalTotalButChargeOnlySale()
    {
        var order=Order.CreateDraft(Guid.NewGuid(),null,UserId,Now);
        var line=order.AddLine(Guid.NewGuid(),"Budín Proteico Chocolate",10,2,1,15,15,null,"Producto defectuoso",null,"Degustación en punto de venta",null,UserId,Now);

        Assert.Equal(13,line.Quantity);
        Assert.Equal(10,line.SaleQuantity);
        Assert.Equal(2,line.ReplacementQuantity);
        Assert.Equal(1,line.TastingQuantity);
        Assert.Equal(150,line.LineTotal);
    }

    [Fact]
    public void RemovingConfirmedLinePreservesItAndReleasesReservation()
    {
        var order = Order.CreateDraft(Guid.NewGuid(), null, UserId, Now);
        order.Configure(order.CustomerId, null, null, null, Now.AddDays(1), null, UserId, Now);
        var line = order.AddLine(Guid.NewGuid(), "Budín", 2, 20m, 20m, null, UserId, Now);
        order.Confirm(OrderSnapshot.Create(order.Id, "Cliente", null, null, null, null, null, Now), UserId, Now);

        order.RemoveConfirmedLine(line.Id, "Producto retirado", UserId, Now);

        Assert.False(line.IsActive);
        Assert.False(Assert.Single(order.Reservations).IsActive);
        Assert.Contains(order.ChangeHistory, x => x.Field == "LineRemoved");
    }
}
