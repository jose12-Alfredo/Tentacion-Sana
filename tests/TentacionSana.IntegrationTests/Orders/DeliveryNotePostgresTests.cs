using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using TentacionSana.Application.Orders;
using TentacionSana.Application.Security;
using TentacionSana.Domain.Catalog;
using TentacionSana.Domain.Customers;
using TentacionSana.Domain.Deliveries;
using TentacionSana.Domain.Inventory;
using TentacionSana.Domain.Orders;
using TentacionSana.Infrastructure.Identity;
using TentacionSana.Infrastructure.Orders;
using TentacionSana.Infrastructure.Persistence;

namespace TentacionSana.IntegrationTests.Orders;

public sealed class IsolatedPostgresFactAttribute : FactAttribute
{
    public IsolatedPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TENTACION_THERMAL_TEST_CONNECTION")))
            Skip = "Requiere PostgreSQL real aislado en loopback; configura TENTACION_THERMAL_TEST_CONNECTION.";
    }
}

public sealed class DeliveryNotePostgresTests
{
    [IsolatedPostgresFact]
    public async Task BeforeDeliveryWithoutReceivableAutomaticallyShowsDebtAndRespectsManualHide()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var db = fixture.Open();
        Assert.Empty(await db.Receivables.ToListAsync()); Assert.Empty(await db.Deliveries.ToListAsync());
        var before = await fixture.BusinessSnapshotAsync();
        var automatic = await fixture.Service.PreviewAsync(fixture.OrderId, null, fixture.UserId, showPaymentStatus: null);
        Assert.True(automatic.Succeeded, string.Join(" ", automatic.Errors));
        Assert.True(automatic.Value!.Content.ShowPaymentStatus);
        Assert.Equal("PENDIENTE DE PAGO", automatic.Value.Content.PaymentLabel);
        Assert.Equal("78969873", automatic.Value.Content.CommercialPhone);
        Assert.Equal("58mm-v2.2", automatic.Value.Content.TemplateVersion);
        SaveSample("antes-confirmar-visible", automatic.Value, false);
        var hidden = (await fixture.Service.PreviewAsync(fixture.OrderId, null, fixture.UserId, false)).Value!;
        Assert.False(hidden.Content.ShowPaymentStatus); Assert.Null(hidden.Content.PaymentLabel);
        SaveSample("antes-confirmar-oculto", hidden, false);
        Assert.Equal(before, await fixture.BusinessSnapshotAsync());
        await fixture.AddPaymentAsync(100, createReceivable: false);
        var paidBefore = await fixture.BusinessSnapshotAsync();
        var deposit = (await fixture.Service.PreviewAsync(fixture.OrderId, null, fixture.UserId, showPaymentStatus: null)).Value!;
        Assert.True(deposit.Content.ShowPaymentStatus); Assert.Equal("PENDIENTE DE PAGO", deposit.Content.PaymentLabel);
        SaveSample("antes-confirmar-abono", deposit, false);
        Assert.Equal(paidBefore, await fixture.BusinessSnapshotAsync());
        await fixture.AddPaymentAsync(165, createReceivable: false);
        var covered = (await fixture.Service.PreviewAsync(fixture.OrderId, null, fixture.UserId, showPaymentStatus: null)).Value!;
        Assert.False(covered.Content.ShowPaymentStatus); Assert.Null(covered.Content.PaymentLabel);
        SaveSample("antes-confirmar-cubierto", covered, false);
        Assert.Empty(await db.Receivables.ToListAsync());
        var snapshot = await fixture.BusinessSnapshotAsync();
        var issued = await fixture.Service.IssueAsync(new(fixture.OrderId, null, covered.Fingerprint, Guid.NewGuid(), covered.Content.ShowPaymentStatus), fixture.UserId);
        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));
        Assert.Equal(snapshot, await fixture.BusinessSnapshotAsync());
    }

    [IsolatedPostgresFact]
    public async Task DirectLegacyPaymentsWithoutAllocationsOrReceivableAreNotIgnoredOrCountedTwice()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddPaymentAsync(100, createReceivable: false, allocate: false);
        var before = await fixture.BusinessSnapshotAsync();
        var partial = (await fixture.Service.PreviewAsync(fixture.OrderId, null, fixture.UserId, showPaymentStatus: null)).Value!;
        Assert.Equal("PENDIENTE DE PAGO", partial.Content.PaymentLabel);
        Assert.Equal(before, await fixture.BusinessSnapshotAsync());
        // Este segundo pago sí tiene aplicación; no se debe volver a sumar por Payment.OrderId.
        await fixture.AddPaymentAsync(100, createReceivable: false);
        var remaining = (await fixture.Service.PreviewAsync(fixture.OrderId, null, fixture.UserId, showPaymentStatus: null)).Value!;
        Assert.True(remaining.Content.ShowPaymentStatus); Assert.Equal("PENDIENTE DE PAGO", remaining.Content.PaymentLabel);
        await fixture.AddPaymentAsync(65, createReceivable: false, allocate: false);
        var covered = (await fixture.Service.PreviewAsync(fixture.OrderId, null, fixture.UserId, showPaymentStatus: null)).Value!;
        Assert.False(covered.Content.ShowPaymentStatus); Assert.Null(covered.Content.PaymentLabel);
        await using var db = fixture.Open(); Assert.Empty(await db.Receivables.ToListAsync());
    }

    [IsolatedPostgresFact]
    public async Task AfterConfirmedDeliveryUsesSamePendingLabelAndNeverChangesFinancialData()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var db = fixture.Open();
        var order = await db.Orders.Include(x => x.Lines).Include(x => x.StatusHistory).SingleAsync(x => x.Id == fixture.OrderId);
        var priorHistory = order.StatusHistory.Select(x => x.Id).ToHashSet();
        var line = order.Lines[0]; var now = DateTimeOffset.UtcNow;
        var delivery = Delivery.Create(order.Id, fixture.UserId, now.AddDays(-1), [(line.Id, 15, 1, 2)], fixture.UserId, now.AddDays(-1));
        delivery.Complete(new Dictionary<Guid, (int, int, int)> { [delivery.Lines[0].Id] = (15, 1, 2) }, "Receptor de prueba", fixture.UserId, now);
        order.AdvanceTo(OrderStatus.InPreparation, "Estado de prueba", fixture.UserId, now);
        order.AdvanceTo(OrderStatus.Ready, "Estado de prueba", fixture.UserId, now);
        order.MarkDelivered(fixture.UserId, now);
        db.OrderStatusHistory.AddRange(order.StatusHistory.Where(x => !priorHistory.Contains(x.Id)));
        var receivable = Receivable.Create(order.Id, order.CustomerId, null); receivable.CorrectInvoice(265);
        db.Receivables.Add(receivable); db.Deliveries.Add(delivery); await db.SaveChangesAsync();
        var before = await fixture.BusinessSnapshotAsync();
        var visible = (await fixture.Service.PreviewAsync(order.Id, delivery.Id, fixture.UserId, showPaymentStatus: null)).Value!;
        Assert.True(visible.Content.ShowPaymentStatus); Assert.Equal("PENDIENTE DE PAGO", visible.Content.PaymentLabel);
        Assert.Equal("RegisteredSummary", visible.Content.Scope); Assert.Equal(18, visible.Content.PhysicalUnits);
        var storedCompletedAt = await db.Deliveries.AsNoTracking().Where(x => x.Id == delivery.Id).Select(x => x.CompletedAtUtc).SingleAsync();
        Assert.Equal(storedCompletedAt, visible.Content.CompletedAtUtc);
        SaveSample("despues-confirmar-visible", visible, true);
        var hidden = (await fixture.Service.PreviewAsync(order.Id, delivery.Id, fixture.UserId, false)).Value!;
        SaveSample("despues-confirmar-oculto", hidden, true);
        Assert.Equal(before, await fixture.BusinessSnapshotAsync());
        await fixture.AddPaymentAsync(100);
        var deposit = (await fixture.Service.PreviewAsync(order.Id, delivery.Id, fixture.UserId, showPaymentStatus: null)).Value!;
        Assert.Equal("PENDIENTE DE PAGO", deposit.Content.PaymentLabel); SaveSample("despues-confirmar-abono", deposit, true);
        await fixture.AddPaymentAsync(165);
        var covered = (await fixture.Service.PreviewAsync(order.Id, delivery.Id, fixture.UserId, showPaymentStatus: null)).Value!;
        Assert.False(covered.Content.ShowPaymentStatus); Assert.Null(covered.Content.PaymentLabel);
        SaveSample("despues-confirmar-cubierto", covered, true);
    }

    [IsolatedPostgresFact]
    public async Task FreeOnlyAutomaticOptionIsOffAndCanStillBeIssued()
    {
        await using var fixture = await Fixture.CreateAsync(onlyFree: true);
        var automatic = (await fixture.Service.PreviewAsync(fixture.OrderId, null, fixture.UserId, showPaymentStatus: null)).Value!;
        Assert.False(automatic.Content.ShowPaymentStatus); Assert.Null(automatic.Content.PaymentLabel);
        var result = await fixture.Service.IssueAsync(new(fixture.OrderId, null, automatic.Fingerprint, Guid.NewGuid(), false), fixture.UserId);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
    }

    private static void SaveSample(string name, DeliveryNotePreview preview, bool hasReceivable)
    {
        var output = Environment.GetEnvironmentVariable("TENTACION_THERMAL_SAMPLES");
        if (string.IsNullOrWhiteSpace(output)) return;
        Directory.CreateDirectory(output);
        File.WriteAllBytes(Path.Combine(output, name + ".pdf"), preview.Pdf);
        File.WriteAllBytes(Path.Combine(output, name + ".png"), preview.Png);
        File.WriteAllText(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(new { HasReceivable = hasReceivable, preview.Content }));
    }

    [IsolatedPostgresFact]
    public async Task OnlyFreeProductsNeverShowDebtEvenWhenAnExistingShippingFeeApplies()
    {
        await using var fixture = await Fixture.CreateAsync(onlyFree: true);
        var before = await fixture.BusinessSnapshotAsync();
        var result = await fixture.Service.PreviewAsync(fixture.OrderId, null, fixture.UserId, showPaymentStatus: true);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Equal(0, result.Value!.Content.Subtotal); Assert.Equal(10, result.Value.Content.DeliveryFee);
        Assert.Null(result.Value.Content.PaymentLabel); Assert.Null(result.Value.Content.PaymentWarning);
        Assert.Equal(before, await fixture.BusinessSnapshotAsync());
    }

    [IsolatedPostgresFact]
    public async Task PaymentVisibilityIsSnapshottedAndLaterPaymentsNeverRewriteAnIssuedNote()
    {
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.BusinessSnapshotAsync();
        var hidden = (await fixture.Service.PreviewAsync(fixture.OrderId, null, fixture.UserId)).Value!;
        var visible = (await fixture.Service.PreviewAsync(fixture.OrderId, null, fixture.UserId, showPaymentStatus: true)).Value!;
        Assert.Null(hidden.Content.PaymentLabel); Assert.False(hidden.Content.ShowPaymentStatus);
        Assert.Equal("PENDIENTE DE PAGO", visible.Content.PaymentLabel);
        Assert.NotEqual(hidden.Fingerprint, visible.Fingerprint);
        Assert.Equal(hidden.Content.Total, visible.Content.Total);
        var wrongOption = await fixture.Service.IssueAsync(new(fixture.OrderId, null, visible.Fingerprint, Guid.NewGuid(), false), fixture.UserId);
        Assert.False(wrongOption.Succeeded);
        var issued = await fixture.Service.IssueAsync(new(fixture.OrderId, null, visible.Fingerprint, Guid.NewGuid(), true), fixture.UserId);
        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));
        Assert.Equal(before, await fixture.BusinessSnapshotAsync());
        Assert.False((await fixture.Service.IssueAsync(new(fixture.OrderId, null, visible.Fingerprint, Guid.NewGuid(), false), fixture.UserId)).Succeeded);
        await fixture.AddPaymentAsync(100);
        var afterPayment = await fixture.BusinessSnapshotAsync();
        var partial = (await fixture.Service.PreviewAsync(fixture.OrderId, null, fixture.UserId, showPaymentStatus: true)).Value!;
        Assert.Equal("PENDIENTE DE PAGO", partial.Content.PaymentLabel);
        var original = (await fixture.Service.GetIssuedAsync(issued.Value!.Id, fixture.UserId)).Value!;
        Assert.Equal("PENDIENTE DE PAGO", original.Content.PaymentLabel);
        Assert.True(original.Content.ShowPaymentStatus);
        Assert.Equal(issued.Value.Pdf, original.Pdf); Assert.Equal(issued.Value.Png, original.Png);
        await fixture.AddPaymentAsync(165);
        var paidBefore = await fixture.BusinessSnapshotAsync();
        var paid = (await fixture.Service.PreviewAsync(fixture.OrderId, null, fixture.UserId, showPaymentStatus: true)).Value!;
        Assert.Null(paid.Content.PaymentLabel); Assert.Null(paid.Content.PaymentWarning);
        Assert.Equal(paidBefore, await fixture.BusinessSnapshotAsync());
        Assert.NotEqual(afterPayment, paidBefore);
    }

    [IsolatedPostgresFact]
    public async Task PartialDeliveryShowsOnlyVerifiableDebtAndWarnsAboutOrderLevelPayments()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var db = fixture.Open();
        var order = await db.Orders.Include(x => x.Lines).SingleAsync(x => x.Id == fixture.OrderId);
        var delivery = Delivery.Create(order.Id, fixture.UserId, DateTimeOffset.UtcNow, [(order.Lines[0].Id, 3, 0, 0)], fixture.UserId, DateTimeOffset.UtcNow);
        db.Deliveries.Add(delivery); await db.SaveChangesAsync();
        var unpaid = (await fixture.Service.PreviewAsync(order.Id, delivery.Id, fixture.UserId, showPaymentStatus: true)).Value!;
        Assert.Equal("PENDIENTE DE PAGO", unpaid.Content.PaymentLabel);
        await fixture.AddPaymentAsync(20);
        var before = await fixture.BusinessSnapshotAsync();
        var uncertain = (await fixture.Service.PreviewAsync(order.Id, delivery.Id, fixture.UserId, showPaymentStatus: true)).Value!;
        Assert.Null(uncertain.Content.PaymentLabel); Assert.NotNull(uncertain.Content.PaymentWarning);
        Assert.Contains(uncertain.Content.PaymentWarning, uncertain.Warnings);
        var automatic = (await fixture.Service.PreviewAsync(order.Id, delivery.Id, fixture.UserId, showPaymentStatus: null)).Value!;
        Assert.False(automatic.Content.ShowPaymentStatus); Assert.Null(automatic.Content.PaymentLabel);
        Assert.Contains(automatic.Warnings, x => x.Contains("entrega parcial", StringComparison.Ordinal));
        var autoIssued = await fixture.Service.IssueAsync(new(order.Id, delivery.Id, automatic.Fingerprint, Guid.NewGuid(), false), fixture.UserId);
        Assert.True(autoIssued.Succeeded, string.Join(" ", autoIssued.Errors));
        var issued = await fixture.Service.IssueAsync(new(order.Id, delivery.Id, uncertain.Fingerprint, Guid.NewGuid(), true), fixture.UserId);
        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));
        Assert.Equal(before, await fixture.BusinessSnapshotAsync());
    }

    [IsolatedPostgresFact]
    public async Task PaymentAfterPreviewRequiresRefreshBeforeIssueAndHiddenOriginalStaysHidden()
    {
        await using var fixture = await Fixture.CreateAsync();
        var hidden = (await fixture.Service.PreviewAsync(fixture.OrderId, null, fixture.UserId)).Value!;
        var visible = (await fixture.Service.PreviewAsync(fixture.OrderId, null, fixture.UserId, showPaymentStatus: true)).Value!;
        await fixture.AddPaymentAsync(265);
        Assert.False((await fixture.Service.IssueAsync(new(fixture.OrderId, null, visible.Fingerprint, Guid.NewGuid(), true), fixture.UserId)).Succeeded);
        var issued = await fixture.Service.IssueAsync(new(fixture.OrderId, null, hidden.Fingerprint, Guid.NewGuid()), fixture.UserId);
        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));
        var original = (await fixture.Service.GetIssuedAsync(issued.Value!.Id, fixture.UserId)).Value!;
        Assert.False(original.Content.ShowPaymentStatus); Assert.Null(original.Content.PaymentLabel);
        Assert.Equal(issued.Value.Pdf, original.Pdf);
    }

    [IsolatedPostgresFact]
    public async Task PreviouslyIssuedPartialPaymentLabelAndContactArePreservedExactly()
    {
        await using var fixture = await Fixture.CreateAsync();
        var content = (await fixture.Service.PreviewAsync(fixture.OrderId, null, fixture.UserId)).Value!.Content with
            { TemplateVersion = "58mm-v2.1", ShowPaymentStatus = true, PaymentLabel = "PAGO PARCIAL", CommercialPhone = "Contacto histórico" };
        byte[] pdf = "%PDF-original-v2.1-PAGO PARCIAL"u8.ToArray(), png = [137, 80, 78, 71, 10, 20];
        var note = DeliveryNote.Create(900, fixture.OrderId, null, Guid.NewGuid(), fixture.UserId, content.IssuedAtUtc,
            content.Scope, new string('C', 64), JsonSerializer.Serialize(content), pdf, png);
        await using var db = fixture.Open(); db.DeliveryNotes.Add(note);
        db.Entry(note).Property(x => x.TemplateVersion).CurrentValue = "58mm-v2.1"; await db.SaveChangesAsync();
        var original = (await fixture.Service.GetIssuedAsync(note.Id, fixture.UserId)).Value!;
        Assert.Equal(pdf, original.Pdf); Assert.Equal(png, original.Png);
        Assert.Equal("PAGO PARCIAL", original.Content.PaymentLabel);
        Assert.Equal("Contacto histórico", original.Content.CommercialPhone);
        Assert.Equal("58mm-v2.1", original.Content.TemplateVersion);
    }

    [IsolatedPostgresFact]
    public async Task LegacyV1NotesAreRetrievedWithoutRegeneratingTheirBytesOrLogo()
    {
        await using var fixture = await Fixture.CreateAsync();
        var content = (await fixture.Service.PreviewAsync(fixture.OrderId, null, fixture.UserId)).Value!;
        var json = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(content.Content))!;
        foreach (var key in new[] { "TemplateVersion", "ShowPaymentStatus", "PaymentLabel", "PaymentWarning" }) json.AsObject().Remove(key);
        byte[] oldPdf = "%PDF-legacy-original"u8.ToArray(), oldPng = [137, 80, 78, 71, 1, 2, 3];
        var note = DeliveryNote.Create(800, fixture.OrderId, null, Guid.NewGuid(), fixture.UserId, DateTimeOffset.UtcNow,
            "PendingOrder", new string('B', 64), json.ToJsonString(), oldPdf, oldPng);
        await using var db = fixture.Open(); db.DeliveryNotes.Add(note);
        db.Entry(note).Property(x => x.TemplateVersion).CurrentValue = "58mm-v1";
        await db.SaveChangesAsync();
        var original = (await fixture.Service.GetIssuedAsync(note.Id, fixture.UserId)).Value!;
        Assert.Equal(oldPdf, original.Pdf); Assert.Equal(oldPng, original.Png);
        Assert.Equal("58mm-v1", original.Content.TemplateVersion);
        Assert.False(original.Content.ShowPaymentStatus); Assert.Null(original.Content.PaymentLabel);
        Assert.Equal(content.Content.Logo, original.Content.Logo);
    }

    [IsolatedPostgresFact]
    public async Task MigrationCanBeRolledBackAndReappliedWithoutChangingBusinessTables()
    {
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.BusinessSnapshotAsync();
        await using var db = fixture.Open();
        var migrations = db.Database.GetMigrations().ToList();
        var previous = migrations[migrations.IndexOf("20261126003356_AddThermalDeliveryNotes") - 1];
        await db.GetService<IMigrator>().MigrateAsync(previous);
        Assert.True(await db.Database.SqlQueryRaw<bool>("SELECT to_regclass('tentacion_sana.\"DeliveryNotes\"') IS NULL AS \"Value\"").SingleAsync());
        await db.Database.MigrateAsync();
        Assert.Empty(await db.DeliveryNotes.ToListAsync());
        Assert.Equal(before, await fixture.BusinessSnapshotAsync());
    }

    [IsolatedPostgresFact]
    public async Task AppliesMigrationAndPreviewDoesNotConsumeNumberOrChangeCommercialData()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var db = fixture.Open();
        Assert.Contains("20261126003356_AddThermalDeliveryNotes", await db.Database.GetAppliedMigrationsAsync());
        var before = await fixture.BusinessSnapshotAsync();
        var preview = await fixture.Service.PreviewAsync(fixture.OrderId, null, fixture.UserId);
        Assert.True(preview.Succeeded, string.Join(" ", preview.Errors));
        Assert.Equal(18, preview.Value!.Content.PhysicalUnits);
        Assert.Equal(255, preview.Value.Content.Subtotal);
        Assert.Equal(265, preview.Value.Content.Total);
        Assert.Empty(await db.DeliveryNotes.ToListAsync());
        var called = await db.Database.SqlQueryRaw<bool>("SELECT is_called AS \"Value\" FROM tentacion_sana.\"DeliveryNoteNumberSequence\"").SingleAsync();
        Assert.False(called);
        Assert.Equal(before, await fixture.BusinessSnapshotAsync());
    }

    [IsolatedPostgresFact]
    public async Task ConcurrentIssueDeduplicatesAndReprintsOriginalBytesAfterOrderChanges()
    {
        await using var fixture = await Fixture.CreateAsync();
        var preview = (await fixture.Service.PreviewAsync(fixture.OrderId, null, fixture.UserId)).Value!;
        var before = await fixture.BusinessSnapshotAsync();
        var command = new IssueDeliveryNote(fixture.OrderId, null, preview.Fingerprint, Guid.NewGuid());
        var results = await Task.WhenAll(fixture.Service.IssueAsync(command, fixture.UserId),
            fixture.Service.IssueAsync(command with { RequestId = Guid.NewGuid() }, fixture.UserId));
        Assert.All(results, x => Assert.True(x.Succeeded, string.Join(" ", x.Errors)));
        Assert.Equal(results[0].Value!.Id, results[1].Value!.Id);
        Assert.Equal(before, await fixture.BusinessSnapshotAsync());
        await using var db = fixture.Open();
        Assert.Single(await db.DeliveryNotes.ToListAsync());
        Assert.Single(await db.AuditEntries.Where(x => x.Action == "DeliveryNoteIssued").ToListAsync());
        var original = results[0].Value!;
        var order = await db.Orders.Include(x => x.Lines).Include(x => x.Reservations).Include(x => x.ChangeHistory).SingleAsync(x => x.Id == fixture.OrderId);
        var line = order.Lines[0];
        order.ReviseLine(line.Id, line.ProductId, line.ProductName, 16, 1, 2, 20, 17, "Mayorista", "Acuerdo comercial", null,
            "Promoción", "Cambio posterior", "Edición posterior a la emisión", fixture.UserId, DateTimeOffset.UtcNow);
        db.OrderChangeHistory.Add(order.ChangeHistory[^1]);
        await db.SaveChangesAsync();
        var changed = await fixture.BusinessSnapshotAsync();
        var reprint = await fixture.Service.GetIssuedAsync(original.Id, fixture.UserId);
        Assert.True(reprint.Succeeded);
        Assert.Equal(original.Number, reprint.Value!.Number);
        Assert.Equal(original.Pdf, reprint.Value.Pdf); Assert.Equal(original.Png, reprint.Value.Png);
        Assert.Equal(15, reprint.Value.Content.SaleUnits);
        Assert.Equal(changed, await fixture.BusinessSnapshotAsync());
        var stale = await fixture.Service.IssueAsync(command with { RequestId = Guid.NewGuid(), Fingerprint = new string('A', 64) }, fixture.UserId);
        Assert.False(stale.Succeeded);
        order.Cancel("Cancelación posterior de prueba", fixture.UserId, DateTimeOffset.UtcNow);
        db.OrderStatusHistory.Add(order.StatusHistory[^1]); await db.SaveChangesAsync();
        var cancelled = await fixture.Service.GetContextAsync(order.Id, fixture.UserId);
        Assert.True(cancelled.Succeeded); Assert.Empty(cancelled.Value!.Scopes);
        Assert.Single(cancelled.Value.Issued);
        Assert.Equal(original.Pdf, (await fixture.Service.GetIssuedAsync(original.Id, fixture.UserId)).Value!.Pdf);
    }

    [IsolatedPostgresFact]
    public async Task UniqueNumbersForDifferentOrdersAndPermissionChecksAreEnforced()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = (await fixture.Service.PreviewAsync(fixture.OrderId, null, fixture.UserId)).Value!;
        await using var db = fixture.Open();
        var source = await db.Orders.Include(x => x.Lines).SingleAsync(x => x.Id == fixture.OrderId);
        var second = Order.CreateDraft(source.CustomerId, null, fixture.UserId, DateTimeOffset.UtcNow);
        second.Configure(source.CustomerId, null, null, null, DateTimeOffset.UtcNow, null, fixture.UserId, DateTimeOffset.UtcNow);
        second.AddLine(source.Lines[0].ProductId, "Producto segundo pedido", 1, 20, 20, null, fixture.UserId, DateTimeOffset.UtcNow);
        second.Confirm(OrderSnapshot.Create(second.Id, "Cliente de ejemplo", null, null, null, null, null, DateTimeOffset.UtcNow), fixture.UserId, DateTimeOffset.UtcNow);
        db.Orders.Add(second); await db.SaveChangesAsync();
        var secondPreview = (await fixture.Service.PreviewAsync(second.Id, null, fixture.UserId)).Value!;
        var issued = await Task.WhenAll(fixture.Service.IssueAsync(new(source.Id, null, first.Fingerprint, Guid.NewGuid()), fixture.UserId),
            fixture.Service.IssueAsync(new(second.Id, null, secondPreview.Fingerprint, Guid.NewGuid()), fixture.UserId));
        Assert.All(issued, x => Assert.True(x.Succeeded, string.Join(" ", x.Errors)));
        Assert.NotEqual(issued[0].Value!.Number, issued[1].Value!.Number);
        Assert.False((await fixture.Service.PreviewAsync(fixture.OrderId, null, Guid.NewGuid())).Succeeded);
        Assert.False((await fixture.Service.GetIssuedAsync(issued[0].Value!.Id, Guid.NewGuid())).Succeeded);
        Assert.False((await fixture.Service.IssueAsync(new(source.Id, null, first.Fingerprint, Guid.NewGuid()), Guid.NewGuid())).Succeeded);
        var user = await db.Users.SingleAsync(x => x.Id == fixture.UserId); user.IsEnabled = false; await db.SaveChangesAsync();
        Assert.False((await fixture.Service.GetContextAsync(source.Id, fixture.UserId)).Succeeded);
        user.IsEnabled = true; user.MustChangePassword = true; await db.SaveChangesAsync();
        Assert.False((await fixture.Service.GetContextAsync(source.Id, fixture.UserId)).Succeeded);
    }

    [IsolatedPostgresFact]
    public async Task MultiplePartialDeliveriesSelectOnlyPendingUnitsAndCompletedOrdersUseSavedNotes()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var db = fixture.Open();
        var order = await db.Orders.Include(x => x.Lines).SingleAsync(x => x.Id == fixture.OrderId);
        var line = order.Lines[0]; var now = DateTimeOffset.UtcNow;
        var first = Delivery.Create(order.Id, fixture.UserId, now.AddDays(-1), [(line.Id, 5, 1, 1)], fixture.UserId, now);
        first.Complete(new Dictionary<Guid, (int, int, int)> { [first.Lines[0].Id] = (3, 1, 0) }, "Receptor verificable", fixture.UserId, now);
        var second = Delivery.Create(order.Id, fixture.UserId, now, [(line.Id, 10, 0, 1)], fixture.UserId, now);
        db.Deliveries.AddRange(first, second); await db.SaveChangesAsync();
        var context = await fixture.Service.GetContextAsync(order.Id, fixture.UserId);
        Assert.True(context.Succeeded); Assert.Equal(2, context.Value!.Scopes.Count);
        Assert.False((await fixture.Service.PreviewAsync(order.Id, null, fixture.UserId)).Succeeded);
        var pending = await fixture.Service.PreviewAsync(order.Id, first.Id, fixture.UserId);
        Assert.True(pending.Succeeded, string.Join(" ", pending.Errors));
        Assert.Equal(2, pending.Value!.Content.SaleUnits); Assert.Equal(1, pending.Value.Content.TastingUnits);
        Assert.Equal(0, pending.Value.Content.DeliveryFee); Assert.Null(pending.Value.Content.Receiver);
        var emitted = await fixture.Service.IssueAsync(new(order.Id, first.Id, pending.Value.Fingerprint, Guid.NewGuid()), fixture.UserId);
        Assert.True(emitted.Succeeded, string.Join(" ", emitted.Errors));
        first.Complete(new Dictionary<Guid, (int, int, int)> { [first.Lines[0].Id] = (2, 0, 1) }, "Otro receptor", fixture.UserId, DateTimeOffset.UtcNow);
        db.DeliveryStatusHistory.Add(first.History[^1]);
        await db.SaveChangesAsync();
        var after = await fixture.Service.GetContextAsync(order.Id, fixture.UserId);
        Assert.Contains(after.Value!.Issued, x => x.Id == emitted.Value!.Id);
        Assert.False((await fixture.Service.PreviewAsync(order.Id, first.Id, fixture.UserId)).Succeeded);
        var summary = second;
        summary.Complete(new Dictionary<Guid, (int, int, int)> { [summary.Lines[0].Id] = (10, 0, 1) }, "Recepción de ejemplo", fixture.UserId, DateTimeOffset.UtcNow);
        db.DeliveryStatusHistory.Add(summary.History[^1]);
        await db.SaveChangesAsync();
        var accumulated = await fixture.Service.PreviewAsync(order.Id, summary.Id, fixture.UserId);
        Assert.True(accumulated.Succeeded, string.Join(" ", accumulated.Errors));
        Assert.Equal("RegisteredSummary", accumulated.Value!.Content.Scope);
        Assert.Equal(10, accumulated.Value.Content.SaleUnits);
        Assert.Equal("Recepción de ejemplo", accumulated.Value.Content.Receiver);
        Assert.Equal(0, await db.DeliveryEvidence.CountAsync());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _admin;
        private readonly string _database;
        private readonly DbContextOptions<ApplicationDbContext> _options;
        public Guid UserId { get; }
        public Guid OrderId { get; }
        public DeliveryNoteService Service { get; }
        private Fixture(string admin, string database, DbContextOptions<ApplicationDbContext> options, Guid user, Guid order)
        {
            _admin = admin; _database = database; _options = options; UserId = user; OrderId = order;
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/TentacionSana.Web"));
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Business:WhatsAppNumber"] = "Número de ejemplo" }).Build();
            Service = new(options, TimeProvider.System, Options.Create(new ThermalNoteOptions()), new TestEnvironment { ContentRootPath = root }, config);
        }
        public ApplicationDbContext Open() => new(_options);
        public async Task AddPaymentAsync(decimal amount, bool createReceivable = true, bool allocate = true)
        {
            await using var db = Open();
            var order = await db.Orders.SingleAsync(x => x.Id == OrderId);
            var receivable = await db.Receivables.SingleOrDefaultAsync(x => x.OrderId == OrderId);
            if (receivable is null && createReceivable)
            {
                receivable = Receivable.Create(OrderId, order.CustomerId, null);
                receivable.CorrectInvoice(265); db.Receivables.Add(receivable);
            }
            var payment = Payment.Create(OrderId, null, amount, PaymentMethod.Cash, UserId, null, DateTimeOffset.UtcNow);
            receivable?.ApplyPayment(amount);
            db.Payments.Add(payment);
            if (allocate) db.PaymentAllocations.Add(PaymentAllocation.Create(payment.Id, OrderId, amount, 1, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }
        public static async Task<Fixture> CreateAsync(bool onlyFree = false)
        {
            var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("TENTACION_THERMAL_TEST_CONNECTION"));
            if (builder.Host is not ("127.0.0.1" or "localhost")) throw new InvalidOperationException("Las pruebas solo aceptan PostgreSQL aislado en loopback.");
            builder.Database = "postgres"; var admin = builder.ConnectionString;
            var name = "thermal_note_test_" + Guid.NewGuid().ToString("N");
            await using (var connection = new NpgsqlConnection(admin))
            {
                await connection.OpenAsync(); await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection); await command.ExecuteNonQueryAsync();
            }
            builder.Database = name;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(builder.ConnectionString, x => x.EnableRetryOnFailure()).Options;
            await using var db = new ApplicationDbContext(options);
            await db.Database.MigrateAsync();
            var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "thermal-test", DisplayName = "Usuario de ejemplo", MustChangePassword = false, IsEnabled = true };
            db.Users.Add(user);
            var role = await db.Roles.SingleAsync(x => x.Name == AppRoles.Administrator);
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user.Id, RoleId = role.Id });
            var now = DateTimeOffset.UtcNow;
            var customer = Customer.Create("Cliente de ejemplo", null, null, now);
            var product = Product.Create("Catálogo actual distinto", "thermal-test", "Unidad", now);
            var order = Order.CreateDraft(customer.Id, null, user.Id, now);
            order.Configure(customer.Id, null, null, null, now.AddDays(1), null, user.Id, now);
            order.SetDeliveryFee(10, user.Id, now);
            order.AddLine(product.Id, "Nombre histórico del producto", onlyFree ? 0 : 15, 1, 2, 20, 17, "Mayorista", "Acuerdo comercial", "Autorizado", "Promoción", "Observación para el cliente", user.Id, now);
            order.Confirm(OrderSnapshot.Create(order.Id, customer.Name, null, null, null, null, null, now), user.Id, now);
            var stock = ProductStockBalance.Create(product.Id); stock.Produce(30); order.Reservations[0].Allocate(stock.Reserve(onlyFree ? 3 : 18));
            db.Customers.Add(customer); db.Products.Add(product); db.Orders.Add(order); db.ProductStockBalances.Add(stock);
            await db.SaveChangesAsync();
            return new(admin, name, options, user.Id, order.Id);
        }
        public async Task<string> BusinessSnapshotAsync()
        {
            await using var db = Open();
            var result = new Dictionary<string, string>();
            foreach (var table in new[] { "Orders", "OrderLines", "StockReservations", "ProductStockBalances", "ProductionBatches", "InventoryMovements", "Sales", "Receivables", "Payments", "PaymentAllocations", "Deliveries", "DeliveryLines", "DeliveryEvidence" })
            {
                // Identificadores provenientes exclusivamente de la lista literal anterior.
                var sql = "SELECT COALESCE(md5(string_agg(to_jsonb(t)::text, '' ORDER BY to_jsonb(t)::text)), '') AS \"Value\" FROM tentacion_sana.\"" + table + "\" t";
                result[table] = await db.Database.SqlQueryRaw<string>(sql).SingleAsync();
            }
            return JsonSerializer.Serialize(result);
        }
        public async ValueTask DisposeAsync()
        {
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(_admin); await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE \"{_database}\" WITH (FORCE)", connection); await command.ExecuteNonQueryAsync();
        }
    }
    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "ThermalNotesTests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
