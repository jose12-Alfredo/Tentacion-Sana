namespace TentacionSana.Domain.Requests;

public enum RequestStatus { New, Contacted, Converted, Closed }

public sealed class ProductRequest
{
    private ProductRequest() { }
    public Guid Id { get; private set; }
    public string ContactName { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? Notes { get; private set; }
    public string? Location { get; private set; }
    public string Origin { get; private set; } = "Web";
    public bool ConsentAccepted { get; private set; }
    public RequestStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public Guid? ConvertedCustomerId { get; private set; }
    public Guid? ConvertedOrderId { get; private set; }
    public List<ProductRequestLine> Lines { get; private set; } = [];
    public List<RequestStatusHistory> StatusHistory { get; private set; } = [];

    public static ProductRequest Create(string name, string phone, string? email, string? notes, string? location, string origin, bool consent, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(phone)) throw new ArgumentException("Nombre y teléfono son obligatorios.");
        if (!consent) throw new ArgumentException("Se requiere consentimiento para registrar la solicitud.");
        var request = new ProductRequest { Id = Guid.NewGuid(), ContactName = name.Trim(), Phone = phone.Trim(), Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(), Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(), Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim(), Origin = origin.Trim(), ConsentAccepted = true, Status = RequestStatus.New, CreatedAtUtc = now };
        request.StatusHistory.Add(RequestStatusHistory.Create(request.Id, null, RequestStatus.New, null, "Solicitud creada.", now));
        return request;
    }

    public void AddLine(Guid productId, string productName, int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);
        Lines.Add(new ProductRequestLine(Guid.NewGuid(), Id, productId, productName, quantity));
    }
    public void MarkContacted(Guid userId, string reason, DateTimeOffset now) => ChangeStatus(RequestStatus.Contacted, userId, reason, now);
    public void Close(Guid userId, string reason, DateTimeOffset now) => ChangeStatus(RequestStatus.Closed, userId, reason, now);

    public void MarkConverted(Guid customerId, Guid orderId, Guid userId, DateTimeOffset now)
    {
        if (Status == RequestStatus.Converted) return;
        ConvertedCustomerId = customerId;
        ConvertedOrderId = orderId;
        ChangeStatus(RequestStatus.Converted, userId, "Convertida a pedido borrador.", now);
    }

    public void ReopenAfterOrderDeletion(Guid orderId, Guid userId, DateTimeOffset now)
    {
        if (ConvertedOrderId != orderId) throw new InvalidOperationException("El pedido no pertenece a esta solicitud.");
        var previousStatus = Status;
        ConvertedOrderId = null;
        Status = RequestStatus.Contacted;
        StatusHistory.Add(RequestStatusHistory.Create(Id, previousStatus, Status, userId, "Pedido borrador eliminado; solicitud reabierta.", now));
    }

    private void ChangeStatus(RequestStatus newStatus, Guid userId, string reason, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("El motivo del cambio de estado es obligatorio.");
        if (Status == RequestStatus.Converted) throw new InvalidOperationException("Una solicitud convertida no puede cambiar de estado.");
        if (Status == newStatus) return;
        var previousStatus = Status;
        Status = newStatus;
        StatusHistory.Add(RequestStatusHistory.Create(Id, previousStatus, newStatus, userId, reason, now));
    }
}

public sealed class RequestStatusHistory
{
    private RequestStatusHistory() { }
    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public RequestStatus? PreviousStatus { get; private set; }
    public RequestStatus NewStatus { get; private set; }
    public Guid? ChangedByUserId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset ChangedAtUtc { get; private set; }

    internal static RequestStatusHistory Create(Guid requestId, RequestStatus? previousStatus, RequestStatus newStatus, Guid? userId, string reason, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), RequestId = requestId, PreviousStatus = previousStatus, NewStatus = newStatus,
        ChangedByUserId = userId, Reason = reason.Trim(), ChangedAtUtc = now
    };
}

public sealed class ProductRequestLine
{
    private ProductRequestLine() { }
    internal ProductRequestLine(Guid id, Guid requestId, Guid productId, string productName, int quantity) => (Id, RequestId, ProductId, ProductName, Quantity) = (id, requestId, productId, productName.Trim(), quantity);
    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
}
