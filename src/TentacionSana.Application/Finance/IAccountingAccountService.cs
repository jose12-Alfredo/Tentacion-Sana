namespace TentacionSana.Application.Finance;

public interface IAccountingAccountService
{
    Task<IReadOnlyList<AccountingAccountItem>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<AccountingOperationResult> CreateAsync(CreateAccountingAccountCommand command, Guid userId, CancellationToken cancellationToken = default);
    Task<AccountingOperationResult> SetActiveAsync(Guid accountId, bool isActive, Guid userId, CancellationToken cancellationToken = default);
}

public sealed record CreateAccountingAccountCommand(string Code, string Name, string Kind);
public sealed record AccountingOperationResult(bool Succeeded, Guid? Id, IReadOnlyList<string> Errors);
public sealed record AccountingAccountItem(Guid Id, string Code, string Name, string Kind, bool IsActive, int MovementCount);
