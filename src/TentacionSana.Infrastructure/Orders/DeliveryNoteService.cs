using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using TentacionSana.Application.Deliveries;
using TentacionSana.Application.Orders;
using TentacionSana.Application.Security;
using TentacionSana.Domain.Deliveries;
using TentacionSana.Domain.Orders;
using TentacionSana.Infrastructure.Auditing;
using TentacionSana.Infrastructure.Persistence;

namespace TentacionSana.Infrastructure.Orders;

public sealed class DeliveryNoteService(DbContextOptions<ApplicationDbContext> dbOptions, TimeProvider clock,
    IOptions<ThermalNoteOptions> options, IHostEnvironment environment, IConfiguration configuration) : IDeliveryNoteService
{
    public async Task<NoteResult<DeliveryNoteContext>> GetContextAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var db = new ApplicationDbContext(dbOptions);
        if (!await AuthorizedAsync(db, userId, cancellationToken)) return NoteResult<DeliveryNoteContext>.Fail("No tienes permiso para generar notas de entrega.");
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == orderId && x.ArchivedAtUtc == null, cancellationToken);
        if (order is null) return NoteResult<DeliveryNoteContext>.Fail("El pedido no existe.");
        var issued = await db.DeliveryNotes.AsNoTracking().Where(x => x.OrderId == orderId).OrderByDescending(x => x.Number)
            .Select(x => new DeliveryNoteHistory(x.Id, "NE-" + x.Number.ToString("D6", System.Globalization.CultureInfo.InvariantCulture), x.IssuedAtUtc, x.Scope)).ToListAsync(cancellationToken);
        if (order.Status is OrderStatus.Draft or OrderStatus.Cancelled)
            return issued.Count > 0 ? NoteResult<DeliveryNoteContext>.Ok(new([], issued)) :
                NoteResult<DeliveryNoteContext>.Fail("La nota requiere un pedido confirmado y no cancelado.");
        var notes = await db.DeliveryNotes.AsNoTracking().Where(x => x.OrderId == orderId).Select(x => x.DeliveryId).ToListAsync(cancellationToken);
        var deliveries = await db.Deliveries.AsNoTracking().Include(x => x.Lines).Where(x => x.OrderId == orderId).OrderBy(x => x.ScheduledAtUtc).ToListAsync(cancellationToken);
        var scopes = new List<DeliveryNoteScope>();
        foreach (var delivery in deliveries)
        {
            if (order.Status != OrderStatus.Delivered && (delivery.Status is DeliveryStatus.Scheduled or DeliveryStatus.Assigned or DeliveryStatus.InRoute or DeliveryStatus.Partial) && delivery.Lines.Any(x => x.PendingQuantity > 0))
                scopes.Add(new(delivery.Id, $"Salida {DeliveryDates.LocalDate(delivery.ScheduledAtUtc):dd/MM/yyyy} · {delivery.Lines.Sum(x => x.PendingQuantity)} unidades pendientes"));
            if (delivery.Status == DeliveryStatus.Delivered && !notes.Contains(delivery.Id) && !notes.Contains(null))
                scopes.Add(new(delivery.Id, $"Resumen registrado {DeliveryDates.LocalDate(delivery.CompletedAtUtc ?? delivery.ScheduledAtUtc):dd/MM/yyyy} · cantidades acumuladas"));
        }
        if (order.Status != OrderStatus.Delivered && deliveries.All(x => x.Status is not (DeliveryStatus.Scheduled or DeliveryStatus.Assigned or DeliveryStatus.InRoute or DeliveryStatus.Partial)))
            scopes.Add(new(null, "Unidades pendientes del pedido, sin entrega asignada"));
        return NoteResult<DeliveryNoteContext>.Ok(new(scopes, issued));
    }

    public Task<NoteResult<DeliveryNotePreview>> PreviewAsync(Guid orderId, Guid? deliveryId, Guid userId, CancellationToken cancellationToken = default) =>
        PreviewAsync(orderId, deliveryId, userId, false, cancellationToken);

    public async Task<NoteResult<DeliveryNotePreview>> PreviewAsync(Guid orderId, Guid? deliveryId, Guid userId, bool showPaymentStatus, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = new ApplicationDbContext(dbOptions);
            if (!await AuthorizedAsync(db, userId, cancellationToken)) return NoteResult<DeliveryNotePreview>.Fail("No tienes permiso para generar notas de entrega.");
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
                var content = await ReadContentAsync(db, orderId, deliveryId, showPaymentStatus, cancellationToken);
                var files = ThermalDeliveryNoteDocument.Generate(content, "VISTA PREVIA");
                var warnings = new List<string>();
                if (content.PaymentWarning is { } paymentWarning) warnings.Add(paymentWarning);
                if (content.Scope == "RegisteredSummary") warnings.Add("Es un resumen acumulado de datos registrados; no reconstruye recepciones parciales anteriores.");
                if (content.Scope == "RegisteredSummary" && content.DeliveryFee == 0) warnings.Add("El resumen no atribuye cargos de envío históricos a una entrega cuando no hay una asignación individual verificable.");
                if (string.IsNullOrWhiteSpace(content.CommercialPhone)) warnings.Add("El número de contacto comercial no está configurado; se omitirá del pie.");
                warnings.Add("48 mm imprimibles, 203 DPI y 384 px son parámetros iniciales estimados; falta validarlos con la impresora real.");
                return NoteResult<DeliveryNotePreview>.Ok(new(content, Fingerprint(content), files.Pdf, files.Png, warnings));
            });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException)
        { return NoteResult<DeliveryNotePreview>.Fail(ex.Message); }
    }

    public async Task<NoteResult<DeliveryNoteFiles>> IssueAsync(IssueDeliveryNote command, Guid userId, CancellationToken cancellationToken = default)
    {
        if (command.RequestId == Guid.Empty) return NoteResult<DeliveryNoteFiles>.Fail("La identificación de la emisión es obligatoria.");
        await using var db = new ApplicationDbContext(dbOptions);
        if (!await AuthorizedAsync(db, userId, cancellationToken)) return NoteResult<DeliveryNoteFiles>.Fail("No tienes permiso para emitir notas de entrega.");
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                var previous = await db.DeliveryNotes.AsNoTracking().SingleOrDefaultAsync(x => x.RequestId == command.RequestId, cancellationToken)
                    ?? await db.DeliveryNotes.AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == command.OrderId && x.Fingerprint == command.Fingerprint, cancellationToken);
                if (previous is not null)
                {
                    if (previous.OrderId != command.OrderId || previous.DeliveryId != command.DeliveryId || previous.Fingerprint != command.Fingerprint ||
                        JsonSerializer.Deserialize<ThermalNoteContent>(previous.SnapshotJson)?.ShowPaymentStatus != command.ShowPaymentStatus)
                        return NoteResult<DeliveryNoteFiles>.Fail("La solicitud de emisión ya pertenece a otro contenido.");
                    return NoteResult<DeliveryNoteFiles>.Ok(Files(previous));
                }
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
                var content = await ReadContentAsync(db, command.OrderId, command.DeliveryId, command.ShowPaymentStatus, cancellationToken);
                var fingerprint = Fingerprint(content);
                if (fingerprint != command.Fingerprint)
                    return NoteResult<DeliveryNoteFiles>.Fail("Los datos del pedido, entrega o configuración cambiaron. Genera una nueva vista previa antes de emitir.");
                var sequence = await db.Database.SqlQuery<long>($"SELECT nextval('tentacion_sana.\"DeliveryNoteNumberSequence\"') AS \"Value\"").SingleAsync(cancellationToken);
                var number = $"NE-{sequence:D6}";
                var rendered = ThermalDeliveryNoteDocument.Generate(content, number);
                var note = DeliveryNote.Create(sequence, command.OrderId, command.DeliveryId, command.RequestId, userId,
                    content.IssuedAtUtc, content.Scope, fingerprint, JsonSerializer.Serialize(content), rendered.Pdf, rendered.Png);
                db.DeliveryNotes.Add(note);
                db.AuditEntries.Add(new AuditEntry { Id = Guid.NewGuid(), UserId = userId, Action = "DeliveryNoteIssued", EntityType = "DeliveryNote",
                    EntityId = note.Id.ToString(), OccurredAtUtc = clock.GetUtcNow(), NewValuesJson = JsonSerializer.Serialize(new { number, note.OrderId, note.DeliveryId, fingerprint }) });
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return NoteResult<DeliveryNoteFiles>.Ok(Files(note));
            });
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            var existing = await db.DeliveryNotes.AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == command.OrderId && x.Fingerprint == command.Fingerprint, cancellationToken);
            return existing is null ? NoteResult<DeliveryNoteFiles>.Fail("La emisión compitió con otra solicitud. Reintenta con la misma vista previa.") : NoteResult<DeliveryNoteFiles>.Ok(Files(existing));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException)
        { return NoteResult<DeliveryNoteFiles>.Fail(ex.Message); }
    }

    public async Task<NoteResult<DeliveryNoteFiles>> GetIssuedAsync(Guid noteId, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var db = new ApplicationDbContext(dbOptions);
        if (!await AuthorizedAsync(db, userId, cancellationToken)) return NoteResult<DeliveryNoteFiles>.Fail("No tienes permiso para consultar esta nota.");
        var note = await db.DeliveryNotes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == noteId, cancellationToken);
        return note is null ? NoteResult<DeliveryNoteFiles>.Fail("La nota emitida no existe.") : NoteResult<DeliveryNoteFiles>.Ok(Files(note));
    }

    private async Task<ThermalNoteContent> ReadContentAsync(ApplicationDbContext db, Guid orderId, Guid? deliveryId, bool showPaymentStatus, CancellationToken ct)
    {
        var settings = options.Value;
        if (settings.PaperWidthMm is < 40 or > 80 || settings.PrintableWidthMm < 30 || settings.PrintableWidthMm > settings.PaperWidthMm || settings.PixelWidth is < 128 or > 1024)
            throw new ArgumentException("La configuración de ancho térmico no es válida.");
        var order = await db.Orders.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == orderId && x.ArchivedAtUtc == null, ct)
            ?? throw new ArgumentException("El pedido no existe.");
        if (order.Status is OrderStatus.Draft or OrderStatus.Cancelled) throw new ArgumentException("Solo se puede emitir una nota de un pedido confirmado, no cancelado.");
        var rows = await db.Deliveries.AsNoTracking().Include(x => x.Lines).Where(x => x.OrderId == orderId).OrderBy(x => x.Id).ToListAsync(ct);
        var driverIds = rows.Where(x => x.DriverUserId != null).Select(x => x.DriverUserId!.Value).ToList();
        var drivers = await db.Users.AsNoTracking().Where(x => driverIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
        var deliveries = rows.Select(x => new NoteDeliverySource(x.Id, x.Status.ToString(), x.Version, x.ScheduledAtUtc, x.CompletedAtUtc,
            x.DriverUserId is { } driver ? drivers.GetValueOrDefault(driver) : null, x.ReceiverName,
            x.Lines.Select(l => new NoteDeliveryLineSource(l.OrderLineId, l.AssignedSaleQuantity, l.AssignedReplacementQuantity, l.AssignedTastingQuantity,
                l.DeliveredSaleQuantity, l.DeliveredReplacementQuantity, l.DeliveredTastingQuantity)).ToList())).ToList();
        var selected = deliveryId is { } id ? deliveries.SingleOrDefault(x => x.Id == id) ?? throw new ArgumentException("La entrega no pertenece a este pedido.") : null;
        var summary = selected?.Status == "Delivered";
        if (order.Status == OrderStatus.Delivered && !summary) throw new ArgumentException("Selecciona una nota emitida o una entrega registrada verificable.");
        if (summary && await db.DeliveryNotes.AnyAsync(x => x.OrderId == orderId && (x.DeliveryId == deliveryId || x.DeliveryId == null), ct))
            throw new ArgumentException("Esta entrega ya tiene una nota emitida. Consulta y reimprime su archivo original.");
        var sourceVersion = $"{order.Version}:" + string.Join(';', deliveries.Select(x => $"{x.Id}:{x.Version}"));
        var orderLines = order.Lines.Where(x => x.IsActive).OrderBy(x => x.Id).Select(x => new NoteOrderLineSource(x.Id, x.ProductName, x.SaleQuantity,
            x.ReplacementQuantity, x.TastingQuantity, x.SoldUnitPrice, x.ReplacementReason, x.ReplacementNotes, x.TastingReason, x.TastingNotes)).ToList();
        var lines = DeliveryNoteProjection.SelectLines(orderLines, deliveries, deliveryId, summary);
        var fee = DeliveryNoteProjection.DeliveryFee(order.DeliveryFee, deliveries, deliveryId, summary);
        var priorContent = await db.DeliveryNotes.AsNoTracking().Where(x => x.OrderId == orderId).Select(x => x.SnapshotJson).ToListAsync(ct);
        if (priorContent.Select(x => JsonSerializer.Deserialize<ThermalNoteContent>(x)!).Any(x => x.DeliveryFee > 0 &&
            (x.DeliveryId != deliveryId || x.SourceVersion != sourceVersion))) fee = 0;
        var customer = await db.Customers.AsNoTracking().SingleAsync(x => x.Id == order.CustomerId, ct);
        var point = order.DeliveryPointId is { } pid ? await db.DeliveryPoints.AsNoTracking().SingleOrDefaultAsync(x => x.Id == pid, ct) : null;
        var contact = order.ContactId is { } cid ? await db.CustomerContacts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == cid, ct) : null;
        var path = Path.IsPathRooted(settings.LogoPath) ? settings.LogoPath : Path.Combine(environment.ContentRootPath, settings.LogoPath);
        if (!File.Exists(path)) throw new ArgumentException("No se encontró el logotipo térmico configurado. Revisa ThermalNotes:LogoPath.");
        var logo = await File.ReadAllBytesAsync(path, ct);
        var content = new ThermalNoteContent(order.Id, order.Number, deliveryId, summary ? "RegisteredSummary" : selected is null ? "PendingOrder" : "PendingDelivery", sourceVersion,
            clock.GetUtcNow(), customer.Name, point?.Label, point?.Address, contact?.Phone ?? customer.Phone, selected?.Driver,
            summary ? selected?.Receiver : null, selected?.ScheduledAtUtc ?? order.PromisedAtUtc,
            summary ? selected?.CompletedAtUtc : null, lines, fee, configuration["Business:WhatsAppNumber"], settings.PixelWidth,
            settings.PaperWidthMm, settings.PrintableWidthMm, logo) { TemplateVersion = "58mm-v2.1" };
        if (!showPaymentStatus) return content;
        var allocated = await db.PaymentAllocations.AsNoTracking().Where(x => x.OrderId == orderId &&
            db.Payments.Any(p => p.Id == x.PaymentId && p.Status == PaymentStatus.Confirmed)).SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
        var receivablePaid = await db.Receivables.AsNoTracking().Where(x => x.OrderId == orderId).Select(x => (decimal?)x.PaidAmount).SingleOrDefaultAsync(ct);
        var wholeOrder = lines.Count == orderLines.Count && orderLines.All(x => lines.Any(l => l.OrderLineId == x.Id &&
            l.SaleQuantity == x.Sale && l.ReplacementQuantity == x.Replacement && l.TastingQuantity == x.Tasting)) && fee == order.DeliveryFee;
        var orderTotal = order.Lines.Where(x => x.IsActive).Sum(x => x.LineTotal) + order.DeliveryFee;
        var payment = DeliveryNotePaymentProjection.Evaluate(true, content.SaleUnits == 0 ? 0 : content.Total, orderTotal, allocated, wholeOrder, receivablePaid);
        return content with { ShowPaymentStatus = true, PaymentLabel = payment.Label, PaymentWarning = payment.Warning };
    }
    private static string Fingerprint(ThermalNoteContent content) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(content with { IssuedAtUtc = DateTimeOffset.UnixEpoch }))));
    private static DeliveryNoteFiles Files(DeliveryNote note) => new(note.Id, $"NE-{note.Number:D6}",
        JsonSerializer.Deserialize<ThermalNoteContent>(note.SnapshotJson) ?? throw new InvalidOperationException("La nota histórica no contiene datos válidos."), note.Pdf, note.Png);
    private static Task<bool> AuthorizedAsync(ApplicationDbContext db, Guid user, CancellationToken ct) => db.Users.AsNoTracking().AnyAsync(u => u.Id == user && u.IsEnabled && !u.MustChangePassword &&
        db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && (r.Name == AppRoles.Administrator || r.Name == AppRoles.Sales ||
            db.RoleClaims.Any(c => c.RoleId == r.Id && c.ClaimType == AppPermissions.ClaimType && (c.ClaimValue == AppPermissions.Sales || c.ClaimValue == AppPermissions.Administration))))), ct);
}
