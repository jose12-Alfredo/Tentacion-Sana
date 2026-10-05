using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TentacionSana.Application.Finance;
using TentacionSana.Domain.Finance;
using TentacionSana.Infrastructure.Auditing;
using TentacionSana.Infrastructure.Persistence;

namespace TentacionSana.Infrastructure.Finance;

public sealed class AccountingAccountService(ApplicationDbContext db, TimeProvider clock) : IAccountingAccountService
{
    public async Task<IReadOnlyList<AccountingAccountItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.AccountingAccounts.AsNoTracking()
            .OrderBy(x => x.Code)
            .Select(x => new AccountingAccountItem(x.Id, x.Code, x.Name, x.Kind.ToString(), x.IsActive,
                db.CashMovements.Count(m => m.AccountingAccountId == x.Id)))
            .ToListAsync(cancellationToken);

    public async Task<AccountingOperationResult> CreateAsync(CreateAccountingAccountCommand command, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<AccountingAccountKind>(command.Kind, true, out var kind))
            return Fail("Selecciona una naturaleza contable válida.");
        var code = command.Code.Trim().ToUpperInvariant();
        var name = command.Name.Trim();
        var existing = await db.AccountingAccounts.AsNoTracking().Select(x => new { x.Code, x.Name }).ToListAsync(cancellationToken);
        if (existing.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
            return Fail("Ya existe una cuenta con ese código o nombre.");
        try
        {
            var now = clock.GetUtcNow();
            var account = AccountingAccount.Create(code, name, kind, userId, now);
            db.AccountingAccounts.Add(account);
            db.AuditEntries.Add(Audit(userId, "CreateAccountingAccount", account.Id, $"{account.Code} · {account.Name} · {account.Kind}", now));
            await db.SaveChangesAsync(cancellationToken);
            return new(true, account.Id, []);
        }
        catch (ArgumentException exception) { return Fail(exception.Message); }
        catch (DbUpdateException) { return Fail("No se pudo crear la cuenta porque el código o nombre ya está registrado."); }
    }

    public async Task<AccountingOperationResult> SetActiveAsync(Guid accountId, bool isActive, Guid userId, CancellationToken cancellationToken = default)
    {
        var account = await db.AccountingAccounts.SingleOrDefaultAsync(x => x.Id == accountId, cancellationToken);
        if (account is null) return Fail("La cuenta contable no existe.");
        account.SetActive(isActive);
        var now = clock.GetUtcNow();
        db.AuditEntries.Add(Audit(userId, isActive ? "ActivateAccountingAccount" : "DeactivateAccountingAccount", account.Id, account.Name, now));
        await db.SaveChangesAsync(cancellationToken);
        return new(true, account.Id, []);
    }

    private static AuditEntry Audit(Guid userId, string action, Guid id, string detail, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, Action = action, EntityType = nameof(AccountingAccount), EntityId = id.ToString(),
        NewValuesJson = JsonSerializer.Serialize(new { Detail = detail }), Reason = detail, OccurredAtUtc = now
    };

    private static AccountingOperationResult Fail(string error) => new(false, null, [error]);
}
