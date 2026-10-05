namespace TentacionSana.Application.Customers;
public interface IOperationsQueryService { Task<IReadOnlyList<CustomerSummary>> GetCustomersAsync(CancellationToken cancellationToken=default); Task<IReadOnlyList<DraftOrderSummary>> GetDraftOrdersAsync(CancellationToken cancellationToken=default); }
public sealed record CustomerSummary(Guid Id,string Kind,string Name,string Phone,string? Email,int ContactCount,int DeliveryPointCount,int PaymentResponsibleCount,DateTimeOffset CreatedAtUtc);
public sealed record DraftOrderSummary(Guid Id,string CustomerName,string ProductName,int Quantity,decimal UnitPrice,DateTimeOffset CreatedAtUtc,Guid? SourceRequestId);
