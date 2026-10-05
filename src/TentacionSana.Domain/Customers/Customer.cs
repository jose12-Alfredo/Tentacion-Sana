using System.Net.Mail;
using TentacionSana.Domain.Contact;

namespace TentacionSana.Domain.Customers;

public enum CustomerKind { Individual, Company }
public enum CustomerCategory { Individual, Gym, HealthStore, Minimarket, Reseller, Distributor, Other }
public enum DeliveryPointKind { Home, MainLocation, Branch, Franchise, PointOfSale, Warehouse, Other }
public enum PaymentResponsibleKind { Customer, Branch, CentralCompany, Franchisee, ThirdParty }

public sealed class Customer
{
    private Customer() { }
    public Guid Id { get; private set; }
    public CustomerKind Kind { get; private set; }
    public CustomerCategory Category { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Notes { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public List<CustomerContact> Contacts { get; private set; } = [];
    public List<DeliveryPoint> DeliveryPoints { get; private set; } = [];
    public List<PaymentResponsibleParty> PaymentResponsibleParties { get; private set; } = [];

    public static Customer Create(string name, string? phone, string? email, DateTimeOffset now, CustomerKind kind = CustomerKind.Individual, CustomerCategory category = CustomerCategory.Individual, string? notes = null)
    {
        Validate(name, email);
        return new Customer { Id = Guid.NewGuid(), Kind = kind, Category = category, Name = name.Trim(), Phone = NormalizeOptionalPhone(phone), Email = Clean(email), Notes = Clean(notes), IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now };
    }

    public void Update(CustomerKind kind, CustomerCategory category, string name, string? phone, string? email, string? notes, bool isActive, DateTimeOffset now)
    {
        Validate(name, email);
        Kind = kind; Category = category; Name = name.Trim(); Phone = NormalizeOptionalPhone(phone); Email = Clean(email); Notes = Clean(notes); IsActive = isActive; UpdatedAtUtc = now;
    }

    public CustomerContact AddContact(string name, string phone, string? email, string? role, Guid? pointId, bool orders, bool reception, bool payment, bool administration, bool accounting, bool owner, DateTimeOffset now)
    {
        if (pointId is not null && DeliveryPoints.All(x => x.Id != pointId)) throw new ArgumentException("El punto no pertenece al cliente.");
        var contact = CustomerContact.Create(Id, name, phone, email, role, pointId, orders, reception, payment, administration, accounting, owner, now);
        Contacts.Add(contact); return contact;
    }

    public CustomerContact AddContact(string name, string phone, string? email, string? role, DateTimeOffset now) => AddContact(name, phone, email, role, null, false, false, false, false, false, false, now);

    public PaymentResponsibleParty AddPaymentResponsible(PaymentResponsibleKind kind, string name, string phone, string? email, DateTimeOffset now, Guid? contactId = null, string? label = null)
    {
        if (contactId is not null && Contacts.All(x => x.Id != contactId)) throw new ArgumentException("El contacto no pertenece al cliente.");
        var party = PaymentResponsibleParty.Create(Id, kind, name, phone, email, now, contactId, label);
        PaymentResponsibleParties.Add(party); return party;
    }

    public DeliveryPoint AddDeliveryPoint(DeliveryPointKind kind, string label, string address, string? reference, string? location, Guid? contactId, Guid? payerId, DateTimeOffset now, string? notes = null)
    {
        if (contactId is not null && Contacts.All(x => x.Id != contactId)) throw new ArgumentException("El contacto no pertenece al cliente.");
        if (payerId is not null && PaymentResponsibleParties.All(x => x.Id != payerId)) throw new ArgumentException("El responsable de pago no pertenece al cliente.");
        var point = DeliveryPoint.Create(Id, kind, label, address, reference, location, contactId, payerId, now, notes);
        DeliveryPoints.Add(point); return point;
    }

    private static void Validate(string name, string? email)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 160) throw new ArgumentException("El nombre es obligatorio y admite hasta 160 caracteres.");
        if (!string.IsNullOrWhiteSpace(email) && (!MailAddress.TryCreate(email.Trim(), out _) || email.Trim().Length > 256)) throw new ArgumentException("El correo no tiene un formato válido.");
    }
    internal static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    internal static string? NormalizeOptionalPhone(string? value) => string.IsNullOrWhiteSpace(value) ? null : PhoneNumber.Normalize(value);
}

public sealed class CustomerContact
{
    private CustomerContact() { }
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid? DeliveryPointId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? Role { get; private set; }
    public bool IsOrderContact { get; private set; }
    public bool IsReceptionContact { get; private set; }
    public bool IsPaymentContact { get; private set; }
    public bool IsAdministrator { get; private set; }
    public bool IsAccounting { get; private set; }
    public bool IsOwner { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    internal static CustomerContact Create(Guid customerId, string name, string phone, string? email, string? role, Guid? pointId, bool orders, bool reception, bool payment, bool administration, bool accounting, bool owner, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("El nombre del contacto es obligatorio.");
        return new CustomerContact { Id = Guid.NewGuid(), CustomerId = customerId, DeliveryPointId = pointId, Name = name.Trim(), Phone = PhoneNumber.Normalize(phone), Email = Customer.Clean(email), Role = Customer.Clean(role), IsOrderContact = orders, IsReceptionContact = reception, IsPaymentContact = payment, IsAdministrator = administration, IsAccounting = accounting, IsOwner = owner, IsActive = true, CreatedAtUtc = now };
    }
}

public sealed class PaymentResponsibleParty
{
    private PaymentResponsibleParty() { }
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid? ContactId { get; private set; }
    public PaymentResponsibleKind Kind { get; private set; }
    public string? Label { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    internal static PaymentResponsibleParty Create(Guid customerId, PaymentResponsibleKind kind, string name, string phone, string? email, DateTimeOffset now, Guid? contactId = null, string? label = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("El nombre del responsable de pago es obligatorio.");
        return new PaymentResponsibleParty { Id = Guid.NewGuid(), CustomerId = customerId, ContactId = contactId, Kind = kind, Label = Customer.Clean(label), Name = name.Trim(), Phone = PhoneNumber.Normalize(phone), Email = Customer.Clean(email), IsActive = true, CreatedAtUtc = now };
    }
}

public sealed class DeliveryPoint
{
    private DeliveryPoint() { }
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public DeliveryPointKind Kind { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public string? Reference { get; private set; }
    public string? Location { get; private set; }
    public string? Notes { get; private set; }
    public Guid? ContactId { get; private set; }
    public Guid? PaymentResponsiblePartyId { get; private set; }
    public string? ImagePublicId { get; private set; }
    public string? ImageFormat { get; private set; }
    public string? ImageFileName { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    internal static DeliveryPoint Create(Guid customerId, DeliveryPointKind kind, string label, string address, string? reference, string? location, Guid? contactId, Guid? payerId, DateTimeOffset now, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(address)) throw new ArgumentException("El nombre y la dirección del punto son obligatorios.");
        ValidateLocation(location);
        return new DeliveryPoint { Id = Guid.NewGuid(), CustomerId = customerId, Kind = kind, Label = label.Trim(), Address = address.Trim(), Reference = Customer.Clean(reference), Location = Customer.Clean(location), Notes = Customer.Clean(notes), ContactId = contactId, PaymentResponsiblePartyId = payerId, IsActive = true, CreatedAtUtc = now };
    }

    public void Update(DeliveryPointKind kind, string label, string address, string? reference, string? location, string? notes, Guid? contactId, Guid? payerId, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(address)) throw new ArgumentException("El nombre y la dirección del punto son obligatorios.");
        ValidateLocation(location);
        Kind = kind; Label = label.Trim(); Address = address.Trim(); Reference = Customer.Clean(reference); Location = Customer.Clean(location); Notes = Customer.Clean(notes); ContactId = contactId; PaymentResponsiblePartyId = payerId; IsActive = isActive;
    }

    public void SetImage(string publicId, string format, string fileName) { ImagePublicId = publicId; ImageFormat = format; ImageFileName = fileName; }
    private static void ValidateLocation(string? location) { if (location?.Trim().Length > 1000) throw new ArgumentException("La ubicación no puede superar 1000 caracteres."); }
}
