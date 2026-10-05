using Microsoft.EntityFrameworkCore;
using TentacionSana.Application.Requests;
using TentacionSana.Domain.Requests;
using TentacionSana.Infrastructure.Idempotency;
using TentacionSana.Infrastructure.Persistence;
using TentacionSana.Domain.Customers;
using TentacionSana.Domain.Orders;
using TentacionSana.Infrastructure.Auditing;
using TentacionSana.Domain.Contact;
using Npgsql;

namespace TentacionSana.Infrastructure.Requests;

public sealed class ProductRequestService(ApplicationDbContext db, TimeProvider timeProvider) : IProductRequestService
{
    public async Task<CreateRequestResult> CreateAsync(CreateRequestCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey)) return new(false, null, ["La solicitud no tiene identificador."]);
        var existing = await db.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(x => x.Operation == "CreateProductRequest" && x.Key == command.IdempotencyKey, cancellationToken);
        if (existing is not null) return new(true, Guid.Parse(existing.ResponseJson!), []);
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == command.ProductId && x.IsActive
                 && db.ProductPublications.Any(p => p.ProductId == x.Id && p.IsPublished), cancellationToken);
        if (product is null) return new(false, null, ["El producto seleccionado no está disponible."]);
        try
        {
            var now = timeProvider.GetUtcNow();
            ValidateInput(command.Email, command.Notes, command.Location, command.Quantity);
            var request = ProductRequest.Create(command.ContactName, PhoneNumber.Normalize(command.Phone), command.Email, command.Notes, command.Location, "Web", command.ConsentAccepted, now);
            request.AddLine(product.Id, product.Name, command.Quantity);
            db.ProductRequests.Add(request);
            db.IdempotencyRecords.Add(new IdempotencyRecord { Id = Guid.NewGuid(), Operation = "CreateProductRequest", Key = command.IdempotencyKey, ResponseJson = request.Id.ToString(), CreatedAtUtc = now, ExpiresAtUtc = now.AddDays(1) });
            db.AuditEntries.Add(CreateAuditEntry(null, "CreateProductRequest", request.Id, null, "New", "Solicitud pública creada.", now));
            await db.SaveChangesAsync(cancellationToken);
            return new(true, request.Id, []);
        }
        catch (ArgumentException exception) { return new(false, null, [exception.Message]); }
    }

    public async Task<CreateRequestResult> CreateManualAsync(CreateManualRequestCommand command, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == command.ProductId && x.IsActive, cancellationToken);
        if (product is null) return new(false, null, ["El producto seleccionado no está disponible."]);

        try
        {
            var now = timeProvider.GetUtcNow();
            ValidateInput(command.Email, command.Notes, command.Location, command.Quantity);
            var request = ProductRequest.Create(command.ContactName, PhoneNumber.Normalize(command.Phone), command.Email, command.Notes, command.Location, "WhatsApp", true, now);
            request.AddLine(product.Id, product.Name, command.Quantity);
            db.ProductRequests.Add(request);
            db.AuditEntries.Add(CreateAuditEntry(command.UserId, "CreateManualProductRequest", request.Id, null, "New", "Conversación iniciada en WhatsApp.", now));
            await db.SaveChangesAsync(cancellationToken);
            return new(true, request.Id, []);
        }
        catch (ArgumentException exception)
        {
            return new(false, null, [exception.Message]);
        }
    }

    public async Task<IReadOnlyList<RequestSummary>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.ProductRequests.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new RequestSummary(x.Id, x.ContactName, x.Phone, x.Location, x.Origin, x.Lines.Select(l => l.ProductName).First(), x.Lines.Select(l => l.Quantity).First(), x.Status.ToString(), x.CreatedAtUtc, x.ConvertedOrderId)).ToListAsync(cancellationToken);

    public async Task<CreateRequestResult> ConvertAsync(Guid requestId, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var request = await db.ProductRequests.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (request is null) return new(false, null, ["La solicitud no existe."]);
        if (request.ConvertedOrderId is not null) return new(true, request.ConvertedOrderId, []);
        var now = timeProvider.GetUtcNow();
        var customer = await db.Customers.FirstOrDefaultAsync(x => x.Phone == request.Phone, cancellationToken) ?? Customer.Create(request.ContactName, request.Phone, request.Email, now);
        if (db.Entry(customer).State == EntityState.Detached) db.Customers.Add(customer);
        var order = Order.CreateDraft(customer.Id, request.Id, userId, now);
        foreach (var line in request.Lines)
        {
            var price = await db.ProductPrices.Where(x => x.ProductId == line.ProductId && x.EffectiveFromUtc <= now && (x.EffectiveToUtc == null || x.EffectiveToUtc > now)).OrderByDescending(x => x.EffectiveFromUtc).Select(x => x.Amount).FirstOrDefaultAsync(cancellationToken);
            if (price <= 0) return new(false, null, [$"El producto {line.ProductName} no tiene un precio vigente válido."]);
            order.AddLine(line.ProductId, line.ProductName, line.Quantity, price, price, null, userId, now);
        }
        db.Orders.Add(order);
        request.MarkConverted(customer.Id, order.Id, userId, now);
        db.RequestStatusHistory.Add(request.StatusHistory[^1]);
        db.AuditEntries.Add(CreateAuditEntry(userId, "ConvertProductRequest", request.Id, "New", "Converted", "Convertida a pedido borrador.", now));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(true, order.Id, []);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            var existingOrderId = await db.Orders.AsNoTracking().Where(x => x.SourceRequestId == requestId).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken);
            return existingOrderId is not null
                ? new(true, existingOrderId, [])
                : new(false, null, ["La solicitud fue modificada por otro usuario. Actualiza la bandeja e inténtalo nuevamente."]);
        }
    }

    public async Task<CreateRequestResult> ChangeStatusAsync(Guid requestId, string status, string reason, Guid userId, CancellationToken cancellationToken = default)
    {
        var request = await db.ProductRequests.SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (request is null) return new(false, null, ["La solicitud no existe."]);

        var previousStatus = request.Status.ToString();
        var now = timeProvider.GetUtcNow();
        try
        {
            switch (status)
            {
                case "Contacted":
                    request.MarkContacted(userId, reason, now);
                    break;
                case "Closed":
                    request.Close(userId, reason, now);
                    break;
                default:
                    return new(false, null, ["El estado solicitado no es válido."]);
            }

            db.RequestStatusHistory.Add(request.StatusHistory[^1]);
            db.AuditEntries.Add(CreateAuditEntry(userId, "ChangeProductRequestStatus", request.Id, previousStatus, request.Status.ToString(), reason, now));
            await db.SaveChangesAsync(cancellationToken);
            return new(true, request.Id, []);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return new(false, null, [exception.Message]);
        }
    }

    public async Task<IReadOnlyList<RequestStatusHistoryItem>> GetHistoryAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        await db.RequestStatusHistory.AsNoTracking()
            .Where(x => x.RequestId == requestId)
            .OrderByDescending(x => x.ChangedAtUtc)
            .Select(x => new RequestStatusHistoryItem(
                x.PreviousStatus == null ? null : x.PreviousStatus.ToString(),
                x.NewStatus.ToString(),
                x.Reason,
                x.ChangedAtUtc,
                x.ChangedByUserId))
            .ToListAsync(cancellationToken);

    public async Task<CreateRequestResult> DeleteAsync(Guid requestId, Guid userId, CancellationToken cancellationToken = default)
    {
        var request = await db.ProductRequests.SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (request is null) return new(false, null, ["La solicitud no existe o ya fue eliminada."]);

        if (request.ConvertedOrderId is not null ||
            await db.Orders.AsNoTracking().AnyAsync(x => x.SourceRequestId == requestId, cancellationToken))
        {
            return new(false, null, ["No se puede eliminar una solicitud vinculada a un pedido. Elimina primero el pedido borrador."]);
        }

        var idempotencyRecords = await db.IdempotencyRecords
            .Where(x => x.Operation == "CreateProductRequest" && x.ResponseJson == requestId.ToString())
            .ToListAsync(cancellationToken);
        db.IdempotencyRecords.RemoveRange(idempotencyRecords);
        db.ProductRequests.Remove(request);
        db.AuditEntries.Add(CreateAuditEntry(userId, "DeleteProductRequest", request.Id,
            request.Status.ToString(), "Deleted", "Solicitud eliminada por el usuario.", timeProvider.GetUtcNow()));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new(true, request.Id, []);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return new(false, null, ["No se pudo eliminar la solicitud porque tiene información relacionada."]);
        }
    }

    private static AuditEntry CreateAuditEntry(Guid? userId, string action, Guid requestId, string? previousStatus, string newStatus, string reason, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, Action = action, EntityType = nameof(ProductRequest), EntityId = requestId.ToString(),
        PreviousValuesJson = previousStatus is null ? null : $"{{\"Status\":\"{previousStatus}\"}}",
        NewValuesJson = $"{{\"Status\":\"{newStatus}\"}}", Reason = reason, OccurredAtUtc = now
    };

    private static void ValidateInput(string? email, string? notes, string? location, int quantity)
    {
        if (!string.IsNullOrWhiteSpace(email) && (!System.Net.Mail.MailAddress.TryCreate(email.Trim(), out _) || email.Trim().Length > 256))
            throw new ArgumentException("El correo no tiene un formato válido.");
        if (notes?.Trim().Length > 1500) throw new ArgumentException("Las observaciones no pueden superar 1500 caracteres.");
        if (location?.Trim().Length > 1000) throw new ArgumentException("La ubicación no puede superar 1000 caracteres.");
        if (quantity is < 1 or > 1000) throw new ArgumentException("La cantidad debe estar entre 1 y 1000.");
    }
}
