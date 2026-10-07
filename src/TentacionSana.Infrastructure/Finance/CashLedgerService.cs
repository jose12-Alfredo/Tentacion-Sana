using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TentacionSana.Application.Finance;
using TentacionSana.Domain.Finance;
using TentacionSana.Infrastructure.Auditing;
using TentacionSana.Infrastructure.Media;
using TentacionSana.Infrastructure.Persistence;

namespace TentacionSana.Infrastructure.Finance;

public sealed class CashLedgerService(ApplicationDbContext db, IOptions<CloudinaryOptions> options, TimeProvider clock, IHttpClientFactory httpClientFactory) : ICashLedgerService
{
    private static readonly string[] AllowedTypes = ["image/jpeg", "image/png", "image/webp"];
    private IQueryable<CashMovement> ActiveMovements => db.CashMovements.Where(movement =>
        movement.PaymentId == null || db.Payments.Any(payment => payment.Id == movement.PaymentId &&
            db.Orders.Any(order => order.Id == payment.OrderId && order.ArchivedAtUtc == null)));
    public async Task<CashDashboard> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var rows = await ActiveMovements.AsNoTracking().OrderByDescending(x => x.OccurredAtUtc).Take(200).ToListAsync(cancellationToken);
        var all = await ActiveMovements.AsNoTracking().Select(x => new { x.Account, x.Direction, x.Source, x.OccurredAtUtc, x.Amount }).ToListAsync(cancellationToken);
        decimal Balance(CashAccount account) => all.Where(x => x.Account == account).Sum(x => x.Direction == CashDirection.Income ? x.Amount : -x.Amount);
        var localNow=clock.GetLocalNow();var periodStart=new DateTimeOffset(localNow.Year,localNow.Month,1,0,0,0,localNow.Offset).ToUniversalTime();
        var business=all.Where(x=>x.OccurredAtUtc>=periodStart&&x.Source is CashSource.CustomerPayment or CashSource.InventoryPurchase or CashSource.Manual).ToList();
        var paymentIds=rows.Where(x=>x.PaymentId!=null).Select(x=>x.PaymentId!.Value).ToList();
        var paymentOrders=await db.Payments.AsNoTracking().Where(x=>paymentIds.Contains(x.Id)).Select(x=>new{x.Id,x.OrderId}).ToDictionaryAsync(x=>x.Id,cancellationToken);
        var orderIds=paymentOrders.Values.Select(x=>x.OrderId).Distinct().ToList();
        var orderLabels=await db.Orders.AsNoTracking().Where(x=>orderIds.Contains(x.Id)).Select(x=>new{x.Id,x.Number,Customer=db.Customers.Where(c=>c.Id==x.CustomerId).Select(c=>c.Name).FirstOrDefault(),Point=x.DeliveryPointId==null?null:db.DeliveryPoints.Where(p=>p.Id==x.DeliveryPointId).Select(p=>p.Label).FirstOrDefault()}).ToDictionaryAsync(x=>x.Id,cancellationToken);
        string Detail(CashMovement x){if(x.PaymentId is not {} paymentId||!paymentOrders.TryGetValue(paymentId,out var link)||!orderLabels.TryGetValue(link.OrderId,out var order))return x.Detail;return $"Pedido #{order.Number} · {order.Customer}"+(string.IsNullOrWhiteSpace(order.Point)?"":$" · {order.Point}");}
        var payables = await db.CashPayables.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).Take(100)
            .Select(x => new CashPayableItem(x.Id, x.PersonName, x.Amount, x.CreatedAtUtc, x.Status.ToString())).ToListAsync(cancellationToken);
        var income=business.Where(x=>x.Direction==CashDirection.Income).Sum(x=>x.Amount);var expense=business.Where(x=>x.Direction==CashDirection.Expense).Sum(x=>x.Amount);
        return new(Balance(CashAccount.Bank), Balance(CashAccount.Cash), income,expense,income-expense,
            rows.Select(x => new CashMovementItem(x.Id,x.Account.ToString(),x.Direction.ToString(),x.Source.ToString(),x.Category,x.OccurredAtUtc,Detail(x),x.Amount,x.TransferId,x.AccountingAccountId)).ToList(), payables);
    }

    public async Task<CashResult> RegisterManualAsync(ManualCashMovementCommand command, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<CashAccount>(command.Account,true,out var account)) return Fail("Selecciona banco/QR o caja/efectivo.");
        if (!Enum.TryParse<CashDirection>(command.Direction,true,out var direction)) return Fail("Selecciona entrada o salida.");
        var accountingAccount = await db.AccountingAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == command.AccountingAccountId && x.IsActive, cancellationToken);
        if (accountingAccount is null) return Fail("Selecciona una cuenta contable activa.");
        var acceptsIncome = accountingAccount.Kind is AccountingAccountKind.Income or AccountingAccountKind.Liability or AccountingAccountKind.Equity;
        if ((direction == CashDirection.Income) != acceptsIncome)
            return Fail(direction == CashDirection.Income ? "La cuenta seleccionada no admite entradas." : "La cuenta seleccionada no admite salidas.");
        var upload = await Upload(command.EvidenceContent,command.EvidenceFileName,command.EvidenceContentType,command.EvidenceLength,"manual",cancellationToken);
        if (!upload.Ok) return Fail(upload.Error!);
        try { var now=clock.GetUtcNow(); var movement=CashMovement.Create(account,direction,CashSource.Manual,command.OccurredAtUtc,command.Detail,command.Amount,upload.PublicId!,command.EvidenceFileName,upload.Format!,upload.Bytes,userId,now,category:accountingAccount.Name,accountingAccountId:accountingAccount.Id);db.CashMovements.Add(movement);await db.SaveChangesAsync(cancellationToken);return new(true,movement.Id,[]); }
        catch(ArgumentException ex){return Fail(ex.Message);}
    }

    public async Task<CashResult> TransferAsync(TransferCashCommand command,Guid userId,CancellationToken cancellationToken=default)
    {
        if(!Enum.TryParse<CashAccount>(command.FromAccount,true,out var from)||!Enum.TryParse<CashAccount>(command.ToAccount,true,out var to))return Fail("Selecciona cuentas válidas.");
        if(from==to)return Fail("El origen y el destino deben ser diferentes.");
        var upload=await Upload(command.EvidenceContent,command.EvidenceFileName,command.EvidenceContentType,command.EvidenceLength,"transferencias",cancellationToken);if(!upload.Ok)return Fail(upload.Error!);
        var transferId=Guid.NewGuid();var now=clock.GetUtcNow();
        try{db.CashMovements.Add(CashMovement.Create(from,CashDirection.Expense,CashSource.Transfer,command.OccurredAtUtc,command.Detail,command.Amount,upload.PublicId!,command.EvidenceFileName,upload.Format!,upload.Bytes,userId,now,category:"Transferencia",transferId:transferId));db.CashMovements.Add(CashMovement.Create(to,CashDirection.Income,CashSource.Transfer,command.OccurredAtUtc,command.Detail,command.Amount,upload.PublicId!,command.EvidenceFileName,upload.Format!,upload.Bytes,userId,now,category:"Transferencia",transferId:transferId));await db.SaveChangesAsync(cancellationToken);return new(true,transferId,[]);}catch(ArgumentException ex){return Fail(ex.Message);}
    }

    public async Task<CashResult> RegisterCashCountAsync(CashCountCommand command,Guid userId,CancellationToken cancellationToken=default)
    {
        var expected=await ActiveMovements.Where(x=>x.Account==CashAccount.Cash).SumAsync(x=>x.Direction==CashDirection.Income?x.Amount:-x.Amount,cancellationToken);var difference=decimal.Round(command.CountedAmount-expected,2);
        (bool Ok,string? PublicId,string? Format,long Bytes,string? Error) upload=(true,null,null,0,null);
        if(command.EvidenceContent is not null)upload=await Upload(command.EvidenceContent,command.EvidenceFileName!,command.EvidenceContentType!,command.EvidenceLength,"arqueos",cancellationToken);
        if(!upload.Ok)return Fail(upload.Error!);
        try{var count=CashCount.Create(command.CountedAtUtc,expected,command.CountedAmount,command.Observation,upload.PublicId,command.EvidenceFileName,upload.Format,upload.Bytes,userId);db.CashCounts.Add(count);if(difference!=0)db.CashMovements.Add(CashMovement.Create(CashAccount.Cash,difference>0?CashDirection.Income:CashDirection.Expense,CashSource.CashCountAdjustment,command.CountedAtUtc,$"Ajuste por arqueo: {command.Observation}",Math.Abs(difference),upload.PublicId!,command.EvidenceFileName!,upload.Format!,upload.Bytes,userId,clock.GetUtcNow(),category:"Ajuste de arqueo"));await db.SaveChangesAsync(cancellationToken);return new(true,count.Id,[]);}catch(ArgumentException ex){return Fail(ex.Message);}
    }

    public async Task<CashResult> UpdateMovementAsync(UpdateCashMovementCommand command, Guid userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Trim().Length > 500) return Fail("Indica un motivo de corrección de hasta 500 caracteres.");
        if (!Enum.TryParse<CashAccount>(command.Account, true, out var account) || !Enum.IsDefined(account)) return Fail("Selecciona una cuenta de dinero válida.");
        if (command.Amount <= 0 || decimal.Round(command.Amount, 2) <= 0) return Fail("El monto debe ser mayor que cero.");
        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                var movement = await db.CashMovements.SingleOrDefaultAsync(x => x.Id == command.MovementId, cancellationToken);
                if (movement is null) return Fail("El movimiento ya no existe.");
                if (movement.Source is not (CashSource.Manual or CashSource.Transfer)) return Fail("Este movimiento se corrige desde su operación de origen.");
                if (movement.Source == CashSource.Transfer && movement.TransferId is null) return Fail("La transferencia no tiene un vínculo válido.");
                var related = movement.Source == CashSource.Transfer
                    ? await db.CashMovements.Where(x => x.TransferId == movement.TransferId).ToListAsync(cancellationToken)
                    : [movement];
                var before = JsonSerializer.Serialize(related.Select(Snapshot).ToList());
                if (movement.Source == CashSource.Manual)
                {
                    if (!Enum.TryParse<CashDirection>(command.Direction, true, out var direction) || !Enum.IsDefined(direction)) return Fail("Selecciona entrada o salida.");
                    var accountingAccount = await db.AccountingAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == command.AccountingAccountId && x.IsActive, cancellationToken);
                    if (accountingAccount is null) return Fail("Selecciona una cuenta contable activa.");
                    var acceptsIncome = accountingAccount.Kind is AccountingAccountKind.Income or AccountingAccountKind.Liability or AccountingAccountKind.Equity;
                    if ((direction == CashDirection.Income) != acceptsIncome) return Fail("La cuenta contable no corresponde al tipo de movimiento.");
                    movement.CorrectManual(account, direction, command.OccurredAtUtc, command.Detail, command.Amount, accountingAccount.Id, accountingAccount.Name);
                }
                else
                {
                    if (!Enum.TryParse<CashAccount>(command.OtherAccount, true, out var other) || !Enum.IsDefined(other) || account == other)
                        return Fail("Selecciona un origen y un destino diferentes.");
                    if (related.Count != 2 || related.Count(x => x.Direction == CashDirection.Expense) != 1 || related.Count(x => x.Direction == CashDirection.Income) != 1)
                        return Fail("La transferencia está incompleta y no puede modificarse.");
                    related.Single(x => x.Direction == CashDirection.Expense).CorrectTransfer(account, CashDirection.Expense, command.OccurredAtUtc, command.Detail, command.Amount);
                    related.Single(x => x.Direction == CashDirection.Income).CorrectTransfer(other, CashDirection.Income, command.OccurredAtUtc, command.Detail, command.Amount);
                }
                db.AuditEntries.Add(new AuditEntry { Id = Guid.NewGuid(), UserId = userId, Action = "CashMovementCorrected", EntityType = "CashMovement", EntityId = command.MovementId.ToString(), PreviousValuesJson = before, NewValuesJson = JsonSerializer.Serialize(related.Select(Snapshot).ToList()), Reason = command.Reason.Trim(), OccurredAtUtc = clock.GetUtcNow() });
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new CashResult(true, command.MovementId, []);
            });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return Fail(ex is DbUpdateException ? "No se pudo guardar la corrección. Intenta nuevamente." : ex.Message);
        }
    }

    public async Task<CashResult> DeleteMovementAsync(DeleteCashMovementCommand command, Guid userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Trim().Length > 500) return Fail("Indica un motivo de eliminación de hasta 500 caracteres.");
        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                var movement = await db.CashMovements.SingleOrDefaultAsync(x => x.Id == command.MovementId, cancellationToken);
                if (movement is null) return Fail("El movimiento ya no existe.");
                if (movement.Source is not (CashSource.Manual or CashSource.Transfer)) return Fail("Este movimiento se elimina desde su operación de origen.");
                if (movement.Source == CashSource.Transfer && movement.TransferId is null) return Fail("La transferencia no tiene un vínculo válido.");
                var related = movement.Source == CashSource.Transfer
                    ? await db.CashMovements.Where(x => x.TransferId == movement.TransferId).ToListAsync(cancellationToken)
                    : [movement];
                if (movement.Source == CashSource.Transfer && (related.Count != 2 || related.Count(x => x.Direction == CashDirection.Expense) != 1 || related.Count(x => x.Direction == CashDirection.Income) != 1))
                    return Fail("La transferencia está incompleta y no puede eliminarse.");
                db.AuditEntries.Add(new AuditEntry { Id = Guid.NewGuid(), UserId = userId, Action = "CashMovementDeleted", EntityType = "CashMovement", EntityId = command.MovementId.ToString(), PreviousValuesJson = JsonSerializer.Serialize(related.Select(Snapshot).ToList()), Reason = command.Reason.Trim(), OccurredAtUtc = clock.GetUtcNow() });
                db.CashMovements.RemoveRange(related);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new CashResult(true, command.MovementId, []);
            });
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return Fail("No se pudo eliminar el movimiento. Intenta nuevamente.");
        }
    }

    private static object Snapshot(CashMovement x) => new { x.Id, x.Account, x.Direction, x.Source, x.OccurredAtUtc, x.Detail, x.Amount, x.Category, x.TransferId, x.AccountingAccountId, x.EvidencePublicId };

    public async Task<CashEvidenceResult> GetEvidenceUrlAsync(Guid movementId,Guid userId,CancellationToken cancellationToken=default)
    {
        var exists=await db.CashMovements.AsNoTracking().AnyAsync(x=>x.Id==movementId,cancellationToken);
        return exists?new(true,$"/media/cash-evidence/{movementId:N}",[]):new(false,null,["El movimiento no existe."]);
    }

    public async Task<CashEvidenceContentResult> GetEvidenceContentAsync(Guid movementId,Guid userId,CancellationToken cancellationToken=default)
    {
        var evidence=await db.CashMovements.AsNoTracking().Where(x=>x.Id==movementId).Select(x=>new{x.EvidencePublicId,x.EvidenceFormat,x.EvidenceFileName}).SingleOrDefaultAsync(cancellationToken);
        if(evidence is null)return new(false,null,null,["El movimiento no existe."]);
        var downloaded=await CloudinaryPrivateMedia.DownloadAsync(options.Value,httpClientFactory,evidence.EvidencePublicId,evidence.EvidenceFormat,evidence.EvidenceFileName,clock.GetUtcNow().AddMinutes(5),cancellationToken);
        return downloaded is null?new(false,null,null,["No se pudo recuperar el respaldo."]):new(true,downloaded.Value.Content,downloaded.Value.ContentType,[]);
    }

    private async Task<(bool Ok,string? PublicId,string? Format,long Bytes,string? Error)> Upload(Stream content,string name,string type,long length,string folder,CancellationToken cancellationToken)
    {
        if(!AllowedTypes.Contains(type.ToLowerInvariant())||length<=0||length>10*1024*1024)return(false,null,null,0,"El respaldo debe ser JPG, PNG o WebP de hasta 10 MB.");
        var result=await Client().UploadAsync(new ImageUploadParams{File=new FileDescription(Path.GetFileName(name),content),Folder=$"tentacion-sana/arqueo/{folder}",Type="authenticated",UniqueFilename=true,Overwrite=false,UseFilename=true},cancellationToken);
        return result.Error is null&&!string.IsNullOrWhiteSpace(result.PublicId)?(true,result.PublicId,result.Format,result.Bytes,null):(false,null,null,0,result.Error?.Message??"No se pudo guardar el respaldo.");
    }
    private Cloudinary Client(){var x=options.Value;return new(new Account(x.CloudName,x.ApiKey,x.ApiSecret)){Api={Secure=true}};}
    private static CashResult Fail(string error)=>new(false,null,[error]);
}

