using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TentacionSana.Application.Receivables;
using TentacionSana.Domain.Deliveries;
using TentacionSana.Infrastructure.Auditing;
using TentacionSana.Infrastructure.Idempotency;
using TentacionSana.Infrastructure.Media;
using TentacionSana.Infrastructure.Persistence;

namespace TentacionSana.Infrastructure.Receivables;

public sealed class ReceivablesService(
    ApplicationDbContext db,
    TimeProvider clock,
    IOptions<CloudinaryOptions> cloudinaryOptions,
    IHttpClientFactory httpClientFactory,
    IHostEnvironment environment) : IReceivablesService
{
    private const long MaximumEvidenceBytes = 10 * 1024 * 1024;
    private static readonly string[] AllowedEvidenceTypes = ["image/jpeg", "image/png", "image/webp"];

    public async Task<ReceivablesOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var rows = await LoadAccountRowsAsync(cancellationToken);
        var orderIds = rows.Select(x => x.OrderId).ToList();
        var deliveryCounts = orderIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await (from evidence in db.DeliveryEvidence.AsNoTracking()
                     join delivery in db.Deliveries.AsNoTracking() on evidence.DeliveryId equals delivery.Id
                     where evidence.IsActive && orderIds.Contains(delivery.OrderId)
                     group evidence by delivery.OrderId into grouped
                     select new { OrderId = grouped.Key, Count = grouped.Count() })
                .ToDictionaryAsync(x => x.OrderId, x => x.Count, cancellationToken);
        var paymentCounts = orderIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await db.PaymentAllocations.AsNoTracking()
                .Where(x => orderIds.Contains(x.OrderId) && db.PaymentEvidence.Any(e => e.PaymentId == x.PaymentId))
                .GroupBy(x => x.OrderId).ToDictionaryAsync(x => x.Key, x => x.Count(), cancellationToken);

        var now = clock.GetUtcNow();
        var groups = rows.GroupBy(x => x.ResponsiblePartyId)
            .Select(group =>
            {
                var first = group.First();
                var orders = group.OrderBy(x => x.DeliveredAtUtc ?? DateTimeOffset.MaxValue).ThenBy(x => x.OrderNumber)
                    .Select(x =>
                    {
                        var age = x.DeliveredAtUtc is null ? 0 : Math.Max(0, (now - x.DeliveredAtUtc.Value).Days);
                        var status = x.Balance <= 0 ? "Paid" : age > 7 ? "Overdue" : x.Paid <= 0 ? "Unpaid" : "Partial";
                        return new ReceivableOrderItem(x.OrderId, x.OrderNumber, x.Customer, x.Branch, x.DeliveredAtUtc,
                            x.Total, x.Paid, x.Balance, x.Version, age, status,
                            deliveryCounts.GetValueOrDefault(x.OrderId), paymentCounts.GetValueOrDefault(x.OrderId));
                    }).ToList();
                return new PayerAccountItem(group.Key, first.PayerName, first.ContactName, first.Phone,
                    group.Select(x => x.Customer).Distinct().OrderBy(x => x).ToList(),
                    group.Sum(x => x.Total), group.Sum(x => x.Paid), group.Sum(x => x.Balance), orders);
            }).OrderByDescending(x => x.Balance).ThenBy(x => x.Name).ToList();

        return new ReceivablesOverview(
            groups.Sum(x => x.Balance),
            groups.Count(x => x.Balance > 0),
            groups.SelectMany(x => x.Orders).Count(x => x.Paid > 0 && x.Balance > 0),
            groups.SelectMany(x => x.Orders).Count(x => x.Status == "Overdue"),
            groups);
    }

    public async Task<PayerAccountHistory?> GetHistoryAsync(Guid responsiblePartyId, CancellationToken cancellationToken = default)
    {
        var overview = await GetOverviewAsync(cancellationToken);
        var payer = overview.ResponsibleParties.SingleOrDefault(x => x.ResponsiblePartyId == responsiblePartyId);
        if (payer is null) return null;
        var payments = await db.Payments.AsNoTracking()
            .Where(x => x.ResponsiblePartyId == responsiblePartyId && x.Status == PaymentStatus.Confirmed)
            .OrderByDescending(x => x.PaymentDateUtc).ToListAsync(cancellationToken);
        var paymentIds = payments.Select(x => x.Id).ToList();
        var allocations = await db.PaymentAllocations.AsNoTracking().Where(x => paymentIds.Contains(x.PaymentId)).ToListAsync(cancellationToken);
        var numbers = await db.Orders.AsNoTracking().Where(x => allocations.Select(a => a.OrderId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Number, cancellationToken);
        var evidence = await db.PaymentEvidence.AsNoTracking().Where(x => paymentIds.Contains(x.PaymentId)).ToDictionaryAsync(x => x.PaymentId, x => (Guid?)x.Id, cancellationToken);
        var paymentItems = payments.Select(payment => new PayerPaymentHistoryItem(payment.Id, payment.Amount, payment.Method.ToString(), payment.PaymentDateUtc, payment.Notes,
            evidence.GetValueOrDefault(payment.Id), allocations.Where(x => x.PaymentId == payment.Id).OrderBy(x => x.ApplicationOrder)
                .Select(x => new AppliedPaymentOrder(x.OrderId, numbers.GetValueOrDefault(x.OrderId), x.Amount)).ToList())).ToList();
        var reports = await db.AccountStatementReports.AsNoTracking().Where(x => x.ResponsiblePartyId == responsiblePartyId)
            .OrderByDescending(x => x.GeneratedAtUtc)
            .Select(x => new AccountStatementHistoryItem(x.Id, x.ReportNumber, x.Type.ToString(), x.BalanceAtGeneration, x.GeneratedAtUtc, x.Orders.Count))
            .ToListAsync(cancellationToken);
        return new PayerAccountHistory(responsiblePartyId, payer.Name, paymentItems, reports);
    }

    public async Task<PayerPaymentResult> RegisterPaymentAsync(RegisterPayerPaymentCommand command, Guid userId, CancellationToken cancellationToken = default)
    {
        var validation = ValidatePayment(command);
        if (validation is not null) return PaymentFailure(validation);
        var existing = await db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(x => x.Operation == "RegisterPayerPayment" && x.Key == command.IdempotencyKey, cancellationToken);
        if (existing is not null && Guid.TryParse(existing.ResponseJson, out var existingId))
            return await ExistingPaymentResultAsync(existingId, command.ResponsiblePartyId, cancellationToken);

        var accountRows = (await LoadAccountRowsAsync(cancellationToken))
            .Where(x => x.ResponsiblePartyId == command.ResponsiblePartyId && x.Balance > 0)
            .OrderBy(x => x.DeliveredAtUtc ?? DateTimeOffset.MaxValue).ThenBy(x => x.OrderNumber).ToList();
        if (accountRows.Count == 0) return PaymentFailure("El responsable ya no tiene saldos pendientes.");
        var previousBalance = accountRows.Sum(x => x.Balance);
        if (command.Amount > previousBalance) return PaymentFailure("El pago supera el saldo pendiente del responsable.");

        List<ManualPaymentAllocation> requested;
        if (command.ApplyAutomatically)
        {
            requested = [];
            var remaining = decimal.Round(command.Amount, 2);
            foreach (var row in accountRows)
            {
                var amount = Math.Min(row.Balance, remaining);
                if (amount > 0) requested.Add(new ManualPaymentAllocation(row.OrderId, amount));
                remaining -= amount;
                if (remaining == 0) break;
            }
        }
        else
        {
            requested = command.Allocations.Where(x => x.Amount > 0).ToList();
            if (requested.Count == 0 || requested.Select(x => x.OrderId).Distinct().Count() != requested.Count)
                return PaymentFailure("Selecciona al menos un pedido y no repitas aplicaciones.");
            if (decimal.Round(requested.Sum(x => x.Amount), 2) != decimal.Round(command.Amount, 2))
                return PaymentFailure("La suma aplicada a pedidos debe coincidir con el pago recibido.");
            foreach (var item in requested)
            {
                var row = accountRows.SingleOrDefault(x => x.OrderId == item.OrderId);
                if (row is null || item.Amount > row.Balance) return PaymentFailure("Una aplicaci\u00F3n supera el saldo disponible del pedido.");
            }
        }

        var settings = cloudinaryOptions.Value;
        if (string.IsNullOrWhiteSpace(settings.CloudName) || string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.ApiSecret))
            return PaymentFailure("Cloudinary no est\u00E1 configurado.");
        var cloudinary = CreateCloudinary(settings);
        var upload = await cloudinary.UploadAsync(new ImageUploadParams
        {
            File = new FileDescription(Path.GetFileName(command.EvidenceFileName), command.EvidenceContent),
            Folder = $"tentacion-sana/pagos/{command.ResponsiblePartyId:N}", Type = "authenticated",
            UniqueFilename = true, Overwrite = false, UseFilename = true
        }, cancellationToken);
        if (upload.Error is not null || string.IsNullOrWhiteSpace(upload.PublicId)) return PaymentFailure(upload.Error?.Message ?? "No se pudo guardar el respaldo del pago.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var orderIds = requested.Select(x => x.OrderId).ToList();
            var receivables = await db.Receivables.Where(x => orderIds.Contains(x.OrderId)).ToListAsync(cancellationToken);
            if (receivables.Count != orderIds.Count) throw new InvalidOperationException("Una cuenta seleccionada ya no existe.");
            foreach (var item in requested)
            {
                var receivable = receivables.Single(x => x.OrderId == item.OrderId);
                receivable.ApplyPayment(item.Amount);
            }
            var now = clock.GetUtcNow();
            var method = command.Method.Equals("Qr", StringComparison.OrdinalIgnoreCase) ? PaymentMethod.Qr : PaymentMethod.Cash;
            var anchorOrderId = requested[0].OrderId;
            var payment = Payment.Create(anchorOrderId, null, command.Amount, method, userId, null, now, command.Notes, command.PaymentDateUtc, command.ResponsiblePartyId);
            db.Payments.Add(payment);
            var position = 1;
            foreach (var item in requested) db.PaymentAllocations.Add(PaymentAllocation.Create(payment.Id, item.OrderId, item.Amount, position++, now));
            db.PaymentEvidence.Add(PaymentEvidence.Create(payment.Id, upload.PublicId, command.EvidenceFileName, upload.Format ?? string.Empty, upload.Bytes, userId, now));
            if (command.CollectedByCurrentUser) db.SettlementObligations.Add(SettlementObligation.Create(payment.Id, userId, payment.Amount, now));
            db.IdempotencyRecords.Add(new IdempotencyRecord { Id = Guid.NewGuid(), Operation = "RegisterPayerPayment", Key = command.IdempotencyKey, ResponseJson = payment.Id.ToString(), CreatedAtUtc = now, ExpiresAtUtc = now.AddDays(30) });
            db.AuditEntries.Add(new AuditEntry { Id = Guid.NewGuid(), UserId = userId, Action = "PayerPaymentRegistered", EntityType = nameof(Payment), EntityId = payment.Id.ToString(), NewValuesJson = JsonSerializer.Serialize(new { command.ResponsiblePartyId, payment.Amount, Method = payment.Method.ToString(), Applications = requested }), Reason = command.Notes, OccurredAtUtc = now });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var orderNumbers = accountRows.ToDictionary(x => x.OrderId, x => x.OrderNumber);
            return new(true, payment.Id, previousBalance, payment.Amount, previousBalance - payment.Amount,
                requested.Select(x => new AppliedPaymentOrder(x.OrderId, orderNumbers[x.OrderId], x.Amount)).ToList(), []);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken); db.ChangeTracker.Clear();
            return PaymentFailure("La informaci\u00F3n cambi\u00F3; recarga e intenta nuevamente.");
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken); db.ChangeTracker.Clear();
            var retry = await db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(x => x.Operation == "RegisterPayerPayment" && x.Key == command.IdempotencyKey, cancellationToken);
            return retry is not null && Guid.TryParse(retry.ResponseJson, out var id)
                ? await ExistingPaymentResultAsync(id, command.ResponsiblePartyId, cancellationToken)
                : PaymentFailure("El pago compiti\u00F3 con otra operaci\u00F3n; recarga e intenta nuevamente.");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            await transaction.RollbackAsync(cancellationToken); return PaymentFailure(exception.Message);
        }
    }

    public async Task<AccountStatementResult> GenerateStatementAsync(GenerateAccountStatementCommand command, Guid userId, CancellationToken cancellationToken = default)
    {
        var type = command.ReportType.Equals("History", StringComparison.OrdinalIgnoreCase) ? AccountStatementReportType.History : AccountStatementReportType.Current;
        var overview = await GetOverviewAsync(cancellationToken);
        var payer = overview.ResponsibleParties.SingleOrDefault(x => x.ResponsiblePartyId == command.ResponsiblePartyId);
        if (payer is null) return StatementFailure("El responsable de pago no existe.");
        Payment? triggerPayment = null;
        var triggerAllocations = new List<PaymentAllocation>();
        if (command.PaymentId is { } paymentId)
        {
            triggerPayment = await db.Payments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == paymentId && x.ResponsiblePartyId == command.ResponsiblePartyId, cancellationToken);
            if (triggerPayment is not null)
                triggerAllocations = await db.PaymentAllocations.AsNoTracking().Where(x => x.PaymentId == paymentId).ToListAsync(cancellationToken);
        }
        var triggerOrderIds = triggerAllocations.Select(x => x.OrderId).ToHashSet();
        var eligibleOrders = payer.Orders.Where(x => command.IncludePaidOrders || x.Balance > 0 || triggerOrderIds.Contains(x.OrderId)).ToList();
        var orders = SelectStatementOrders(eligibleOrders, command.SelectedOrderIds);
        if (command.SelectedOrderIds is { } selectedOrderIds &&
            selectedOrderIds.Distinct().Count() != orders.Count)
            return StatementFailure("La selecci\u00F3n contiene pedidos que no pertenecen al alcance actual del reporte. Actualiza la cuenta e intenta nuevamente.");
        if (orders.Count == 0) return StatementFailure("No hay pedidos que cumplan las opciones del reporte.");
        var currentBalance = orders.Sum(x => x.Balance);
        var selectedPaymentAmount = triggerAllocations.Where(x => orders.Any(order => order.OrderId == x.OrderId)).Sum(x => x.Amount);
        var registeredPayment = triggerPayment is null || selectedPaymentAmount <= 0 ? (decimal?)null : selectedPaymentAmount;
        decimal? previousBalance = registeredPayment is null ? null : currentBalance + registeredPayment.Value;
        var reportPayer = payer with
        {
            Customers = orders.Select(x => x.Customer).Distinct().OrderBy(x => x).ToList(),
            Total = orders.Sum(x => x.Total),
            Paid = orders.Sum(x => x.Paid),
            Balance = currentBalance,
            Orders = orders
        };

        var evidence = await LoadReportEvidenceAsync(orders, command, cancellationToken);
        if (evidence.Errors.Count > 0) return StatementFailure(evidence.Errors.ToArray());
        var movements = type == AccountStatementReportType.History ? await LoadReportMovementsAsync(orders, cancellationToken) : [];
        var now = clock.GetUtcNow();
        var reportNumber = await NextReportNumberAsync(payer.Name, now, cancellationToken);
        var generatedBy = await db.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => x.DisplayName).SingleOrDefaultAsync(cancellationToken) ?? "Usuario";
        var logoPath = Path.Combine(environment.ContentRootPath, "wwwroot", "images", "brand", "logo-oficial-tentacion-sana.png");
        var logo = File.Exists(logoPath) ? await File.ReadAllBytesAsync(logoPath, cancellationToken) : [];
        var model = new AccountStatementPdfModel(reportNumber, now, generatedBy, reportPayer, orders, type.ToString(), previousBalance, registeredPayment, currentBalance, logo, movements, evidence.Items);
        byte[] pdf;
        try { pdf = AccountStatementPdfDocument.Generate(model); }
        catch (Exception exception) { return StatementFailure($"No se pudo componer el PDF: {exception.Message}"); }

        var report = AccountStatementReport.Create(reportNumber, command.ResponsiblePartyId, userId, type, currentBalance, previousBalance, registeredPayment,
            command.IncludeDeliveryEvidence, command.IncludePaymentEvidence, command.IncludePartialPayments, now,
            orders.Select(x => (x.OrderId, x.Total, x.Paid, x.Balance)));
        db.AccountStatementReports.Add(report);
        db.AuditEntries.Add(new AuditEntry { Id = Guid.NewGuid(), UserId = userId, Action = "AccountStatementGenerated", EntityType = nameof(AccountStatementReport), EntityId = report.Id.ToString(), NewValuesJson = JsonSerializer.Serialize(new { report.ReportNumber, report.ResponsiblePartyId, Type = report.Type.ToString(), report.BalanceAtGeneration, Orders = orders.Select(x => x.OrderId) }), OccurredAtUtc = now });
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return StatementFailure("No se pudo registrar la auditor\u00EDa del reporte. Intenta generarlo nuevamente."); }
        return new(true, report.Id, report.ReportNumber, $"{report.ReportNumber}.pdf", pdf, []);
    }

    internal static List<ReceivableOrderItem> SelectStatementOrders(
        IReadOnlyList<ReceivableOrderItem> eligibleOrders,
        IReadOnlyList<Guid>? selectedOrderIds)
    {
        var selected = selectedOrderIds?.Distinct().ToHashSet();
        return eligibleOrders
            .Where(x => selected is null || selected.Contains(x.OrderId))
            .OrderBy(x => x.Customer)
            .ThenBy(x => x.Branch)
            .ThenBy(x => x.OrderNumber)
            .ToList();
    }

    private async Task<List<AccountRow>> LoadAccountRowsAsync(CancellationToken cancellationToken)
    {
        var raw = await db.Receivables.AsNoTracking().Select(receivable => new
        {
            receivable.OrderId,
            OrderNumber = db.Orders.Where(x => x.Id == receivable.OrderId).Select(x => x.Number).First(),
            receivable.CustomerId,
            ResponsiblePartyId = receivable.PayerId ?? receivable.CustomerId,
            PayerName = receivable.PayerId == null ? db.Customers.Where(x => x.Id == receivable.CustomerId).Select(x => x.Name).First() : db.PaymentResponsibleParties.Where(x => x.Id == receivable.PayerId).Select(x => x.Label ?? x.Name).First(),
            Phone = receivable.PayerId == null ? db.Customers.Where(x => x.Id == receivable.CustomerId).Select(x => x.Phone ?? "").First() : db.PaymentResponsibleParties.Where(x => x.Id == receivable.PayerId).Select(x => x.Phone).First(),
            ContactName = receivable.PayerId == null ? null : db.PaymentResponsibleParties.Where(x => x.Id == receivable.PayerId).Select(x => x.ContactId == null ? null : db.CustomerContacts.Where(c => c.Id == x.ContactId).Select(c => c.Name).FirstOrDefault()).FirstOrDefault(),
            Customer = db.Customers.Where(x => x.Id == receivable.CustomerId).Select(x => x.Name).First(),
            Branch = db.Orders.Where(x => x.Id == receivable.OrderId).Select(x => x.DeliveryPointId == null ? "Sin sucursal" : db.DeliveryPoints.Where(p => p.Id == x.DeliveryPointId).Select(p => p.Label).FirstOrDefault() ?? "Sin sucursal").First(),
            DeliveredAtUtc = db.Deliveries.Where(x => x.OrderId == receivable.OrderId && x.CompletedAtUtc != null).Max(x => (DateTimeOffset?)x.CompletedAtUtc),
            Total = receivable.InvoicedAmount,
            Paid = receivable.PaidAmount,
            Balance = receivable.InvoicedAmount > receivable.PaidAmount ? receivable.InvoicedAmount - receivable.PaidAmount : 0,
            receivable.Version
        }).ToListAsync(cancellationToken);
        return raw.Select(x => new AccountRow(x.OrderId, x.OrderNumber, x.CustomerId, x.ResponsiblePartyId, x.PayerName, x.ContactName, x.Phone, x.Customer, x.Branch, x.DeliveredAtUtc, x.Total, x.Paid, x.Balance, x.Version)).ToList();
    }

    private async Task<(List<AccountStatementEvidence> Items, List<string> Errors)> LoadReportEvidenceAsync(IReadOnlyList<ReceivableOrderItem> orders, GenerateAccountStatementCommand command, CancellationToken cancellationToken)
    {
        var items = new List<EvidenceSource>();
        var orderIds = orders.Select(x => x.OrderId).ToList();
        if (command.IncludeDeliveryEvidence)
        {
            items.AddRange(await db.DeliveryEvidence.AsNoTracking()
                .Where(x => x.IsActive && db.Deliveries.Any(d => d.Id == x.DeliveryId && orderIds.Contains(d.OrderId)))
                .Select(x => new EvidenceSource(db.Deliveries.Where(d => d.Id == x.DeliveryId).Select(d => d.OrderId).First(), x.PublicId, x.Format, x.FileName, x.UploadedAtUtc, "BOLETA DE ENTREGA", 1))
                .ToListAsync(cancellationToken));
        }
        if (command.IncludePaymentEvidence || command.IncludePartialPayments)
        {
            var payments = await db.PaymentAllocations.AsNoTracking()
                .Where(x => orderIds.Contains(x.OrderId))
                .Join(db.Payments.Where(x => x.Status == PaymentStatus.Confirmed), allocation => allocation.PaymentId, payment => payment.Id, (allocation, payment) => new { allocation, payment })
                .Join(db.PaymentEvidence, x => x.payment.Id, evidence => evidence.PaymentId, (x, evidence) => new { x.allocation, x.payment, evidence })
                .ToListAsync(cancellationToken);
            foreach (var group in payments.GroupBy(x => x.payment.Id))
            {
                var payment = group.OrderBy(x => orders.Single(order => order.OrderId == x.allocation.OrderId).OrderNumber).First();
                var order = orders.Single(x => x.OrderId == payment.allocation.OrderId);
                var partial = group.Any(x => x.allocation.Amount < orders.Single(orderItem => orderItem.OrderId == x.allocation.OrderId).Total);
                if (!command.IncludePaymentEvidence && !(command.IncludePartialPayments && partial)) continue;
                items.Add(new EvidenceSource(order.OrderId, payment.evidence.PublicId, payment.evidence.Format, payment.evidence.FileName, payment.payment.PaymentDateUtc, partial ? "PAGO PARCIAL" : "COMPROBANTE DE PAGO", partial ? 2 : 3));
            }
        }
        var settings = cloudinaryOptions.Value;
        if (items.Count > 0 && (string.IsNullOrWhiteSpace(settings.CloudName) || string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.ApiSecret)))
            return ([], ["Cloudinary no est\u00E1 configurado para recuperar las evidencias."]);
        var cloudinary = CreateCloudinary(settings);
        var client = httpClientFactory.CreateClient();
        var result = new List<AccountStatementEvidence>();
        var errors = new List<string>();
        var numberByOrder = orders.ToDictionary(x => x.OrderId, x => x.OrderNumber);
        foreach (var source in items.OrderBy(x => numberByOrder[x.OrderId]).ThenBy(x => x.SortOrder).ThenBy(x => x.AtUtc))
        {
            try
            {
                var url = cloudinary.DownloadPrivate(source.PublicId, false, source.Format, "authenticated", clock.GetUtcNow().AddMinutes(10).ToUnixTimeSeconds(), "image", null, source.FileName);
                using var response = await client.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();
                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                result.Add(new AccountStatementEvidence(numberByOrder[source.OrderId], source.Type, source.AtUtc, bytes));
            }
            catch { errors.Add($"No se pudo recuperar la evidencia '{source.FileName}' del pedido #{numberByOrder[source.OrderId]}."); }
        }
        return (result, errors);
    }

    private async Task<List<AccountStatementMovement>> LoadReportMovementsAsync(IReadOnlyList<ReceivableOrderItem> orders, CancellationToken cancellationToken)
    {
        var orderIds = orders.Select(x => x.OrderId).ToList();
        var rows = await db.PaymentAllocations.AsNoTracking().Where(x => orderIds.Contains(x.OrderId))
            .Join(db.Payments.Where(x => x.Status == PaymentStatus.Confirmed), allocation => allocation.PaymentId, payment => payment.Id, (allocation, payment) => new
            {
                payment.PaymentDateUtc, allocation.OrderId,
                OrderNumber = db.Orders.Where(order => order.Id == allocation.OrderId).Select(order => order.Number).First(),
                Method = payment.Method.ToString(), allocation.Amount, payment.Notes
            }).OrderBy(x => x.PaymentDateUtc).ThenBy(x => x.OrderNumber).ToListAsync(cancellationToken);
        var runningPaid = 0m;
        var total = orders.Sum(x => x.Total);
        var movements = new List<AccountStatementMovement>();
        foreach (var row in rows)
        {
            runningPaid += row.Amount;
            movements.Add(new AccountStatementMovement(row.PaymentDateUtc, row.OrderNumber, row.Method, row.Amount, Math.Max(0, total - runningPaid), row.Notes));
        }
        return movements;
    }

    private async Task<string> NextReportNumberAsync(string payerName, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var slug = Slug(payerName);
        var date = now.ToLocalTime().ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var prefix = $"EC-{slug}-{date}-";
        var count = await db.AccountStatementReports.CountAsync(x => x.ReportNumber.StartsWith(prefix), cancellationToken);
        return $"{prefix}{count + 1:000}";
    }

    private async Task<PayerPaymentResult> ExistingPaymentResultAsync(Guid paymentId, Guid responsiblePartyId, CancellationToken cancellationToken)
    {
        var payment = await db.Payments.AsNoTracking().SingleAsync(x => x.Id == paymentId, cancellationToken);
        var applications = await db.PaymentAllocations.AsNoTracking().Where(x => x.PaymentId == paymentId).OrderBy(x => x.ApplicationOrder)
            .Select(x => new AppliedPaymentOrder(x.OrderId, db.Orders.Where(o => o.Id == x.OrderId).Select(o => o.Number).First(), x.Amount)).ToListAsync(cancellationToken);
        var current = (await LoadAccountRowsAsync(cancellationToken)).Where(x => x.ResponsiblePartyId == responsiblePartyId).Sum(x => x.Balance);
        return new(true, payment.Id, current + payment.Amount, payment.Amount, current, applications, []);
    }

    private static string? ValidatePayment(RegisterPayerPaymentCommand command)
    {
        if (command.ResponsiblePartyId == Guid.Empty) return "El responsable de pago es obligatorio.";
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey)) return "La clave de idempotencia es obligatoria.";
        if (command.Amount <= 0) return "El monto debe ser mayor que cero.";
        if (command.Method is not ("Cash" or "Qr")) return "El m\u00E9todo debe ser efectivo o QR.";
        if (command.EvidenceLength <= 0 || command.EvidenceLength > MaximumEvidenceBytes || !AllowedEvidenceTypes.Contains(command.EvidenceContentType.ToLowerInvariant())) return "El respaldo debe ser JPG, PNG o WebP de hasta 10 MB.";
        return null;
    }

    private static string Slug(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var withoutMarks = new string(normalized.Where(x => CharUnicodeInfo.GetUnicodeCategory(x) != UnicodeCategory.NonSpacingMark).ToArray());
        var slug = Regex.Replace(withoutMarks.ToUpperInvariant(), "[^A-Z0-9]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "RESPONSABLE" : slug[..Math.Min(slug.Length, 28)];
    }

    private static Cloudinary CreateCloudinary(CloudinaryOptions settings) => new(new Account(settings.CloudName, settings.ApiKey, settings.ApiSecret)) { Api = { Secure = true } };
    private static PayerPaymentResult PaymentFailure(params string[] errors) => new(false, null, 0, 0, 0, [], errors);
    private static AccountStatementResult StatementFailure(params string[] errors) => new(false, null, null, null, null, errors);

    private sealed record AccountRow(Guid OrderId, long OrderNumber, Guid CustomerId, Guid ResponsiblePartyId, string PayerName, string? ContactName, string Phone, string Customer, string Branch, DateTimeOffset? DeliveredAtUtc, decimal Total, decimal Paid, decimal Balance, int Version);
    private sealed record EvidenceSource(Guid OrderId, string PublicId, string Format, string FileName, DateTimeOffset AtUtc, string Type, int SortOrder);
}
