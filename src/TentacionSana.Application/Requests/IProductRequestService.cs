namespace TentacionSana.Application.Requests;

public interface IProductRequestService
{
    Task<CreateRequestResult> CreateAsync(CreateRequestCommand command, CancellationToken cancellationToken = default);
    Task<CreateRequestResult> CreateManualAsync(CreateManualRequestCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RequestSummary>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<CreateRequestResult> ConvertAsync(Guid requestId, Guid userId, CancellationToken cancellationToken = default);
    Task<CreateRequestResult> ChangeStatusAsync(Guid requestId, string status, string reason, Guid userId, CancellationToken cancellationToken = default);
    Task<CreateRequestResult> DeleteAsync(Guid requestId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RequestStatusHistoryItem>> GetHistoryAsync(Guid requestId, CancellationToken cancellationToken = default);
}

public sealed record CreateRequestCommand(string ContactName, string Phone, string? Email, string? Notes, string? Location, Guid ProductId, int Quantity, bool ConsentAccepted, string IdempotencyKey);
public sealed record CreateManualRequestCommand(string ContactName, string Phone, string? Email, string? Notes, string? Location, Guid ProductId, int Quantity, Guid UserId);
public sealed record CreateRequestResult(bool Succeeded, Guid? RequestId, IReadOnlyList<string> Errors);
public sealed record RequestSummary(Guid Id, string ContactName, string Phone, string? Location, string Origin, string ProductName, int Quantity, string Status, DateTimeOffset CreatedAtUtc, Guid? OrderId);
public sealed record RequestStatusHistoryItem(string? PreviousStatus, string NewStatus, string Reason, DateTimeOffset ChangedAtUtc, Guid? ChangedByUserId);
