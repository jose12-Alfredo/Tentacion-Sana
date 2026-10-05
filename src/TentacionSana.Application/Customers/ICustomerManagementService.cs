namespace TentacionSana.Application.Customers;

public interface ICustomerManagementService
{
    Task<CustomerOperationResult> CreateCustomerAsync(CustomerEditCommand command, CancellationToken cancellationToken = default);
    Task<CustomerOperationResult> UpdateCustomerAsync(Guid customerId, CustomerEditCommand command, CancellationToken cancellationToken = default);
    Task<CustomerOperationResult> AddContactAsync(Guid customerId, ContactEditCommand command, Guid userId, CancellationToken cancellationToken = default);
    Task<CustomerOperationResult> AddPaymentResponsibleAsync(Guid customerId, PaymentResponsibleEditCommand command, Guid userId, CancellationToken cancellationToken = default);
    Task<CustomerOperationResult> AddDeliveryPointAsync(Guid customerId, DeliveryPointEditCommand command, Guid userId, CancellationToken cancellationToken = default);
    Task<CustomerOperationResult> UpdateDeliveryPointAsync(Guid customerId, Guid pointId, DeliveryPointEditCommand command, Guid userId, CancellationToken cancellationToken = default);
    Task<CustomerDetail?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
}

public interface IDeliveryPointImageService
{
    Task<CustomerOperationResult> UploadAsync(Guid customerId, Guid pointId, Stream content, string fileName, string contentType, long length, Guid userId, CancellationToken cancellationToken = default);
    Task<string?> GetUrlAsync(Guid pointId, CancellationToken cancellationToken = default);
    Task<DeliveryPointImageContent?> GetContentAsync(Guid pointId, CancellationToken cancellationToken = default);
}

public sealed record CustomerEditCommand(string Kind, string Category, string Name, string? Phone, string? Email, string? Notes, bool IsActive, Guid UserId, string? DefaultAddress = null, string? DefaultLocation = null);
public sealed record ContactEditCommand(string Name, string Phone, string? Email, string? Role, Guid? DeliveryPointId = null, bool IsOrderContact = false, bool IsReceptionContact = false, bool IsPaymentContact = false, bool IsAdministrator = false, bool IsAccounting = false, bool IsOwner = false);
public sealed record PaymentResponsibleEditCommand(string Kind, string? Label, Guid? ContactId, string? Name = null, string? Phone = null, string? Email = null);
public sealed record DeliveryPointEditCommand(string Kind, string Label, string Address, string? Reference, string? Location, string? Notes, Guid? ContactId, Guid? PaymentResponsiblePartyId, bool IsActive = true);
public sealed record CustomerOperationResult(bool Succeeded, Guid? Id, IReadOnlyList<string> Errors);
public sealed record DeliveryPointImageContent(byte[] Content, string ContentType);
public sealed record CustomerDetail(Guid Id, string Kind, string Category, string Name, string? Phone, string? Email, string? Notes, bool IsActive, IReadOnlyList<ContactDetail> Contacts, IReadOnlyList<PaymentResponsibleDetail> PaymentResponsibles, IReadOnlyList<DeliveryPointDetail> DeliveryPoints);
public sealed record ContactDetail(Guid Id, Guid? DeliveryPointId, string Name, string Phone, string? Email, string? Role, bool IsOrderContact, bool IsReceptionContact, bool IsPaymentContact, bool IsAdministrator, bool IsAccounting, bool IsOwner);
public sealed record PaymentResponsibleDetail(Guid Id, string Kind, string? Label, Guid? ContactId, string Name, string Phone, string? Email, IReadOnlyList<Guid> DeliveryPointIds);
public sealed record DeliveryPointDetail(Guid Id, string Kind, string Label, string Address, string? Reference, string? Location, string? Notes, Guid? ContactId, Guid? PaymentResponsiblePartyId, bool IsActive, bool HasImage);
