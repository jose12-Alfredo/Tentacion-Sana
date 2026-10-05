using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TentacionSana.Application.Customers;
using TentacionSana.Domain.Customers;
using TentacionSana.Infrastructure.Auditing;
using TentacionSana.Infrastructure.Persistence;

#pragma warning disable CA1725

namespace TentacionSana.Infrastructure.Customers;

public sealed class CustomerManagementService(ApplicationDbContext db, TimeProvider timeProvider) : ICustomerManagementService
{
    public async Task<CustomerOperationResult> CreateCustomerAsync(CustomerEditCommand command, CancellationToken ct = default)
    {
        try
        {
            var now = timeProvider.GetUtcNow();
            var customer = Customer.Create(command.Name, command.Phone, command.Email, now, Parse<CustomerKind>(command.Kind), Parse<CustomerCategory>(command.Category), command.Notes);
            db.Customers.Add(customer);
            if (customer.Kind == CustomerKind.Individual && !string.IsNullOrWhiteSpace(command.DefaultAddress))
            {
                var contact = !string.IsNullOrWhiteSpace(customer.Phone) ? customer.AddContact(customer.Name, customer.Phone, customer.Email, "Titular", null, true, true, true, false, false, true, now) : null;
                if (contact is not null) db.CustomerContacts.Add(contact);
                var payer = !string.IsNullOrWhiteSpace(customer.Phone) ? customer.AddPaymentResponsible(PaymentResponsibleKind.Customer, customer.Name, customer.Phone, customer.Email, now, contact?.Id, "Mismo cliente") : null;
                if (payer is not null) db.PaymentResponsibleParties.Add(payer);
                db.DeliveryPoints.Add(customer.AddDeliveryPoint(DeliveryPointKind.Home, "Domicilio principal", command.DefaultAddress, null, command.DefaultLocation, contact?.Id, payer?.Id, now));
            }
            AddAudit(command.UserId, "CreateCustomer", customer.Id, null, new { customer.Kind, customer.Category, customer.Name }, now);
            await db.SaveChangesAsync(ct); return Success(customer.Id);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return Failure(ex.Message); }
    }

    public async Task<CustomerOperationResult> UpdateCustomerAsync(Guid id, CustomerEditCommand command, CancellationToken ct = default)
    {
        var customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (customer is null) return Failure("El cliente no existe.");
        try
        {
            var previous = new { customer.Kind, customer.Category, customer.Name, customer.Phone, customer.Email, customer.Notes, customer.IsActive };
            var now = timeProvider.GetUtcNow();
            customer.Update(Parse<CustomerKind>(command.Kind), Parse<CustomerCategory>(command.Category), command.Name, command.Phone, command.Email, command.Notes, command.IsActive, now);
            AddAudit(command.UserId, "UpdateCustomer", id, previous, command, now);
            await db.SaveChangesAsync(ct); return Success(id);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return Failure(ex.Message); }
    }

    public async Task<CustomerOperationResult> AddContactAsync(Guid id, ContactEditCommand command, Guid userId, CancellationToken ct = default)
    {
        var customer = await Load(id, ct); if (customer is null) return Failure("El cliente no existe.");
        try
        {
            var now = timeProvider.GetUtcNow();
            var item = customer.AddContact(command.Name, command.Phone, command.Email, command.Role, command.DeliveryPointId, command.IsOrderContact, command.IsReceptionContact, command.IsPaymentContact, command.IsAdministrator, command.IsAccounting, command.IsOwner, now);
            db.CustomerContacts.Add(item); AddAudit(userId, "AddCustomerContact", item.Id, null, command, now);
            await db.SaveChangesAsync(ct); return Success(item.Id);
        }
        catch (ArgumentException ex) { return Failure(ex.Message); }
    }

    public async Task<CustomerOperationResult> AddPaymentResponsibleAsync(Guid id, PaymentResponsibleEditCommand command, Guid userId, CancellationToken ct = default)
    {
        var customer = await Load(id, ct); if (customer is null) return Failure("El cliente no existe.");
        try
        {
            var contact = command.ContactId is null ? null : customer.Contacts.SingleOrDefault(x => x.Id == command.ContactId);
            var name = contact?.Name ?? command.Name ?? "";
            var phone = contact?.Phone ?? command.Phone ?? "";
            var email = contact?.Email ?? command.Email;
            var now = timeProvider.GetUtcNow();
            var item = customer.AddPaymentResponsible(Parse<PaymentResponsibleKind>(command.Kind), name, phone, email, now, contact?.Id, command.Label);
            db.PaymentResponsibleParties.Add(item); AddAudit(userId, "AddPaymentResponsible", item.Id, null, command, now);
            await db.SaveChangesAsync(ct); return Success(item.Id);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return Failure(ex.Message); }
    }

    public async Task<CustomerOperationResult> AddDeliveryPointAsync(Guid id, DeliveryPointEditCommand command, Guid userId, CancellationToken ct = default)
    {
        var customer = await Load(id, ct); if (customer is null) return Failure("El cliente no existe.");
        try
        {
            ValidateRelations(customer, command);
            var now = timeProvider.GetUtcNow();
            var item = customer.AddDeliveryPoint(Parse<DeliveryPointKind>(command.Kind), command.Label, command.Address, command.Reference, command.Location, command.ContactId, command.PaymentResponsiblePartyId, now, command.Notes);
            db.DeliveryPoints.Add(item); AddAudit(userId, "AddDeliveryPoint", item.Id, null, command, now);
            await db.SaveChangesAsync(ct); return Success(item.Id);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return Failure(ex.Message); }
    }

    public async Task<CustomerOperationResult> UpdateDeliveryPointAsync(Guid customerId, Guid pointId, DeliveryPointEditCommand command, Guid userId, CancellationToken ct = default)
    {
        var customer = await Load(customerId, ct); if (customer is null) return Failure("El cliente no existe.");
        var point = customer.DeliveryPoints.SingleOrDefault(x => x.Id == pointId); if (point is null) return Failure("El punto no existe.");
        try
        {
            ValidateRelations(customer, command);
            var previous = new { point.Kind, point.Label, point.Address, point.Reference, point.Location, point.ContactId, point.PaymentResponsiblePartyId, point.IsActive };
            point.Update(Parse<DeliveryPointKind>(command.Kind), command.Label, command.Address, command.Reference, command.Location, command.Notes, command.ContactId, command.PaymentResponsiblePartyId, command.IsActive);
            AddAudit(userId, "UpdateDeliveryPoint", point.Id, previous, command, timeProvider.GetUtcNow());
            await db.SaveChangesAsync(ct); return Success(point.Id);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return Failure(ex.Message); }
    }

    public async Task<CustomerDetail?> GetCustomerAsync(Guid id, CancellationToken ct = default)
    {
        var c = await db.Customers.AsNoTracking().AsSplitQuery().Include(x => x.Contacts).Include(x => x.PaymentResponsibleParties).Include(x => x.DeliveryPoints).SingleOrDefaultAsync(x => x.Id == id, ct);
        return c is null ? null : new(c.Id, c.Kind.ToString(), c.Category.ToString(), c.Name, c.Phone, c.Email, c.Notes, c.IsActive,
            c.Contacts.Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new ContactDetail(x.Id, x.DeliveryPointId, x.Name, x.Phone, x.Email, x.Role, x.IsOrderContact, x.IsReceptionContact, x.IsPaymentContact, x.IsAdministrator, x.IsAccounting, x.IsOwner)).ToList(),
            c.PaymentResponsibleParties.Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new PaymentResponsibleDetail(x.Id, x.Kind.ToString(), x.Label, x.ContactId, x.Name, x.Phone, x.Email, c.DeliveryPoints.Where(p => p.PaymentResponsiblePartyId == x.Id).Select(p => p.Id).ToList())).ToList(),
            c.DeliveryPoints.OrderBy(x => x.Label).Select(x => new DeliveryPointDetail(x.Id, x.Kind.ToString(), x.Label, x.Address, x.Reference, x.Location, x.Notes, x.ContactId, x.PaymentResponsiblePartyId, x.IsActive, x.ImagePublicId != null)).ToList());
    }

    private Task<Customer?> Load(Guid id, CancellationToken ct) => db.Customers.AsSplitQuery().Include(x => x.Contacts).Include(x => x.PaymentResponsibleParties).Include(x => x.DeliveryPoints).SingleOrDefaultAsync(x => x.Id == id, ct);
    private static void ValidateRelations(Customer c, DeliveryPointEditCommand command) { if (command.ContactId is not null && c.Contacts.All(x => x.Id != command.ContactId)) throw new ArgumentException("El contacto no pertenece al cliente."); if (command.PaymentResponsiblePartyId is not null && c.PaymentResponsibleParties.All(x => x.Id != command.PaymentResponsiblePartyId)) throw new ArgumentException("El responsable de pago no pertenece al cliente."); }
    private static T Parse<T>(string value) where T : struct, Enum => Enum.TryParse<T>(value, true, out var parsed) ? parsed : throw new ArgumentException("El tipo seleccionado no es válido.");
    private static CustomerOperationResult Success(Guid id) => new(true, id, []);
    private static CustomerOperationResult Failure(string error) => new(false, null, [error]);
    private void AddAudit(Guid userId, string action, Guid id, object? previous, object? current, DateTimeOffset now) => db.AuditEntries.Add(new AuditEntry { Id = Guid.NewGuid(), UserId = userId, Action = action, EntityType = "Customer", EntityId = id.ToString(), PreviousValuesJson = previous is null ? null : JsonSerializer.Serialize(previous), NewValuesJson = current is null ? null : JsonSerializer.Serialize(current), OccurredAtUtc = now });
}
