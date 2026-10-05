using Microsoft.EntityFrameworkCore;
using TentacionSana.Application.Settlements;
using TentacionSana.Domain.Deliveries;
using TentacionSana.Infrastructure.Auditing;
using TentacionSana.Infrastructure.Idempotency;
using TentacionSana.Infrastructure.Persistence;

namespace TentacionSana.Infrastructure.Settlements;

public sealed class SettlementService(ApplicationDbContext db, TimeProvider clock) : ISettlementService
{
    public async Task<IReadOnlyList<SettlementHolderSummary>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.SettlementObligations.AsNoTracking().Where(x => x.SettledAmount < x.Amount)
            .Join(db.Payments, o => o.PaymentId, p => p.Id, (o, p) => new { o, p })
            .Join(db.Orders, x => x.p.OrderId, o => o.Id, (x, order) => new { x.o, x.p, order })
            .Join(db.Customers, x => x.order.CustomerId, c => c.Id, (x, customer) => new { x.o, x.p, x.order, customer })
            .OrderBy(x => x.o.CreatedAtUtc).ToListAsync(cancellationToken);
        var userIds = rows.Select(x => x.o.HolderUserId).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);
        return rows.GroupBy(x => x.o.HolderUserId).Select(group => new SettlementHolderSummary(group.Key, names.GetValueOrDefault(group.Key, "Colaborador"), group.Sum(x => x.o.Amount), group.Sum(x => x.o.SettledAmount), group.Sum(x => x.o.Amount - x.o.SettledAmount), group.Select(x => new SettlementObligationItem(x.o.Id, x.o.PaymentId, x.order.Number, x.customer.Name, x.o.Amount, x.o.SettledAmount, x.o.Amount - x.o.SettledAmount, x.o.Version, x.p.ReceivedAtUtc)).ToList())).OrderByDescending(x => x.Pending).ToList();
    }

    public async Task<IReadOnlyList<SettlementHistoryItem>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.Settlements.AsNoTracking().OrderByDescending(x => x.ReceivedAtUtc).Take(100).ToListAsync(cancellationToken);
        var ids = rows.SelectMany(x => new[] { x.HolderUserId, x.ReceivedByUserId }).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);
        return rows.Select(x => new SettlementHistoryItem(x.Id, names.GetValueOrDefault(x.HolderUserId, "Colaborador"), names.GetValueOrDefault(x.ReceivedByUserId, "Caja"), x.DeclaredAmount, x.ReceivedAmount, x.Difference, x.Status.ToString(), x.DifferenceReason, x.Reference, x.ReceivedAtUtc)).ToList();
    }

    public async Task<SettlementResult> RegisterAsync(RegisterSettlementCommand command, Guid userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey)) return Fail("La clave de idempotencia es obligatoria.");
        var existing = await db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(x => x.Operation == "RegisterSettlement" && x.Key == command.IdempotencyKey, cancellationToken);
        if (existing is not null && Guid.TryParse(existing.ResponseJson, out var existingId)) return Ok(existingId);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var ids = command.Allocations.Select(x => x.ObligationId).Distinct().ToList();
            if (ids.Count != command.Allocations.Count) return Fail("Una obligación no puede repetirse.");
            var obligations = await db.SettlementObligations.Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
            if (obligations.Count != ids.Count) return Fail("Una obligación seleccionada ya no existe.");
            foreach (var allocation in command.Allocations)
            {
                var obligation = obligations.Single(x => x.Id == allocation.ObligationId);
                if (obligation.Version != allocation.ExpectedVersion) return Conflict();
            }
            var now = clock.GetUtcNow();
            var settlement = Settlement.Create(command.HolderUserId, userId, command.ReceivedAmount, command.DifferenceReason, command.Reference, now, command.Allocations.Select(x => (obligations.Single(o => o.Id == x.ObligationId), x.Amount)));
            db.Settlements.Add(settlement);
            db.IdempotencyRecords.Add(new IdempotencyRecord { Id = Guid.NewGuid(), Operation = "RegisterSettlement", Key = command.IdempotencyKey, ResponseJson = settlement.Id.ToString(), CreatedAtUtc = now, ExpiresAtUtc = now.AddDays(30) });
            db.AuditEntries.Add(new AuditEntry { Id = Guid.NewGuid(), UserId = userId, Action = "SettlementRegistered", EntityType = "Settlement", EntityId = settlement.Id.ToString(), NewValuesJson = $"{{\"holderUserId\":\"{command.HolderUserId}\",\"declared\":{settlement.DeclaredAmount},\"received\":{settlement.ReceivedAmount}}}", Reason = settlement.DifferenceReason, OccurredAtUtc = now });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(settlement.Id);
        }
        catch (DbUpdateConcurrencyException) { await transaction.RollbackAsync(cancellationToken); return Conflict(); }
        catch (DbUpdateException) { await transaction.RollbackAsync(cancellationToken); db.ChangeTracker.Clear(); var retry = await db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(x => x.Operation == "RegisterSettlement" && x.Key == command.IdempotencyKey, cancellationToken); return retry is not null && Guid.TryParse(retry.ResponseJson, out var id) ? Ok(id) : Conflict(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { await transaction.RollbackAsync(cancellationToken); return Fail(ex.Message); }
    }

    private static SettlementResult Ok(Guid id) => new(true, id, []);
    private static SettlementResult Fail(string error) => new(false, null, [error]);
    private static SettlementResult Conflict() => new(false, null, ["La información cambió; recarga e intenta nuevamente."], true);
}
