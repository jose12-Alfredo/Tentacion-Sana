using TentacionSana.Domain.Finance;

namespace TentacionSana.UnitTests.Finance;

public sealed class CashLedgerTests
{
    [Fact]
    public void QrCustomerPaymentCreatesBankIncomeWithEvidence()
    {
        var paymentId = Guid.NewGuid();
        var movement = CashMovement.Create(CashAccount.Bank, CashDirection.Income, CashSource.CustomerPayment,
            DateTimeOffset.UtcNow, "Pago pedido 42", 350.456m, "pagos/respaldo", "qr.jpg", "jpg", 1200,
            Guid.NewGuid(), DateTimeOffset.UtcNow, paymentId: paymentId);

        Assert.Equal(CashAccount.Bank, movement.Account);
        Assert.Equal(CashDirection.Income, movement.Direction);
        Assert.Equal(350.46m, movement.Amount);
        Assert.Equal(paymentId, movement.PaymentId);
        Assert.Equal("qr.jpg", movement.EvidenceFileName);
    }

    [Fact]
    public void CashPurchaseCreatesCashExpense()
    {
        var purchaseId = Guid.NewGuid();
        var movement = CashMovement.Create(CashAccount.Cash, CashDirection.Expense, CashSource.InventoryPurchase,
            DateTimeOffset.UtcNow, "Compra de leche", 210m, "compras/foto", "recibo.png", "png", 900,
            Guid.NewGuid(), DateTimeOffset.UtcNow, supplyPurchaseId: purchaseId);

        Assert.Equal(CashAccount.Cash, movement.Account);
        Assert.Equal(CashDirection.Expense, movement.Direction);
        Assert.Equal(purchaseId, movement.SupplyPurchaseId);
    }

    [Fact]
    public void MovementRequiresDetailAmountAndEvidence()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() => CashMovement.Create(CashAccount.Bank,CashDirection.Expense,CashSource.Manual,now,"",10,"foto","x.jpg","jpg",1,Guid.NewGuid(),now));
        Assert.Throws<ArgumentException>(() => CashMovement.Create(CashAccount.Bank,CashDirection.Expense,CashSource.Manual,now,"Motivo",0,"foto","x.jpg","jpg",1,Guid.NewGuid(),now));
        Assert.Throws<ArgumentException>(() => CashMovement.Create(CashAccount.Bank,CashDirection.Expense,CashSource.Manual,now,"Motivo",10,"","x.jpg","jpg",1,Guid.NewGuid(),now));
    }

    [Fact]
    public void RenditionCreatesPendingPayableForPerson()
    {
        var payable = CashPayable.Create(Guid.NewGuid(), "María Pérez", 125.555m, "rendiciones/foto", "ticket.webp", "webp", 500,
            Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Equal("María Pérez", payable.PersonName);
        Assert.Equal(125.56m, payable.Amount);
        Assert.Equal(PayableStatus.Pending, payable.Status);
    }

    [Fact]
    public void RenditionRequiresPersonAndEvidence()
    {
        var now=DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(()=>CashPayable.Create(Guid.NewGuid(),"",10,"foto","x.jpg","jpg",1,Guid.NewGuid(),now));
        Assert.Throws<ArgumentException>(()=>CashPayable.Create(Guid.NewGuid(),"José",10,"","x.jpg","jpg",1,Guid.NewGuid(),now));
    }

    [Fact]
    public void TransferPairKeepsSameAmountAndInternalReference()
    {
        var transferId=Guid.NewGuid();var now=DateTimeOffset.UtcNow;var user=Guid.NewGuid();
        var output=CashMovement.Create(CashAccount.Bank,CashDirection.Expense,CashSource.Transfer,now,"Retiro",80,"foto","x.jpg","jpg",1,user,now,category:"Transferencia",transferId:transferId);
        var input=CashMovement.Create(CashAccount.Cash,CashDirection.Income,CashSource.Transfer,now,"Retiro",80,"foto","x.jpg","jpg",1,user,now,category:"Transferencia",transferId:transferId);
        Assert.Equal(output.Amount,input.Amount);Assert.Equal(transferId,output.TransferId);Assert.Equal(transferId,input.TransferId);
    }

    [Fact]
    public void CashCountRequiresEvidenceOnlyWhenThereIsDifference()
    {
        var now=DateTimeOffset.UtcNow;var user=Guid.NewGuid();
        var balanced=CashCount.Create(now,100,100,"",null,null,null,0,user);
        Assert.Equal(0,balanced.Difference);
        Assert.Throws<ArgumentException>(()=>CashCount.Create(now,100,80,"Faltante",null,null,null,0,user));
        var shortage=CashCount.Create(now,100,80,"Faltante","foto","arqueo.jpg","jpg",1,user);
        Assert.Equal(-20,shortage.Difference);
    }
}
