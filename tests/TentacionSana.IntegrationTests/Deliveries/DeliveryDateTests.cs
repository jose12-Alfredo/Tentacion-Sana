using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using TentacionSana.Application.Deliveries;
using TentacionSana.Application.Security;
using TentacionSana.Domain.Catalog;
using TentacionSana.Domain.Customers;
using TentacionSana.Domain.Deliveries;
using TentacionSana.Domain.Inventory;
using TentacionSana.Domain.Orders;
using TentacionSana.Infrastructure.Dashboard;
using TentacionSana.Infrastructure.Deliveries;
using TentacionSana.Infrastructure.Identity;
using TentacionSana.Infrastructure.Finance;
using TentacionSana.Infrastructure.Media;
using TentacionSana.Infrastructure.Persistence;

namespace TentacionSana.IntegrationTests.Deliveries;

public sealed class DeliveryDateTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 16, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Actual = DeliveryDates.ToUtc(new DateTime(2026, 10, 29));

    [Fact]
    public async Task BackdatedDeliveryUsesReceiptDateForSaleAndReportsButKeepsRegistrationTime()
    {
        await using var fixture = await Fixture.CreateAsync();
        var result = await fixture.CompleteAsync(Actual);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Equal(Actual, (await fixture.Db.Deliveries.SingleAsync()).CompletedAtUtc);
        Assert.Equal(Actual, (await fixture.Db.Sales.SingleAsync()).OccurredAtUtc);
        Assert.Equal(Now, (await fixture.Db.AuditEntries.SingleAsync(x => x.Action == "DeliveryCompleted")).OccurredAtUtc);
        Assert.Equal(Now, (await fixture.Db.DeliveryStatusHistory.SingleAsync(x => x.Reason == "Cantidades entregadas confirmadas")).ChangedAtUtc);
        var detail = await fixture.Service.GetAsync(fixture.DeliveryId);
        Assert.Equal(Actual, detail!.CompletedAtUtc);
        Assert.Equal(Now, detail.RecordedAtUtc);
        var accounts = await fixture.Service.ReceivablesAsync();
        Assert.Equal(Actual, Assert.Single(accounts).DeliveredAtUtc);
        Assert.Equal(1, (await fixture.Service.GetBoardAsync(new(2026, 10, 29))).DeliveredTodayCount);
        Assert.Equal(0, (await fixture.Service.GetBoardAsync(new(2026, 11, 20))).DeliveredTodayCount);

        var analytics = new SalesAnalyticsService(fixture.Db);
        var october = await analytics.GetAsync(new(2026, 10, 1), new(2026, 10, 31));
        var november = await analytics.GetAsync(new(2026, 11, 1), new(2026, 11, 30));
        Assert.Equal(150m, october.Revenue);
        Assert.Equal(0m, november.Revenue);
    }

    [Fact]
    public async Task CorrectingExistingDeliveryMovesReportsAndDebtDateWithoutChangingPaymentOrStock()
    {
        await using var fixture = await Fixture.CreateAsync();
        Assert.True((await fixture.CompleteAsync(Now)).Succeeded);
        var receivable = await fixture.Db.Receivables.SingleAsync();
        receivable.ApplyPayment(50);
        var payment = Payment.Create(fixture.OrderId, fixture.DeliveryId, 50, PaymentMethod.Cash, fixture.UserId, null, Now, paymentDate: Now.AddDays(-2));
        fixture.Db.Payments.Add(payment);
        fixture.Db.PaymentAllocations.Add(PaymentAllocation.Create(payment.Id, fixture.OrderId, 50, 1, Now));
        await fixture.Db.SaveChangesAsync();
        var stockBefore = (await fixture.Db.ProductStockBalances.AsNoTracking().SingleAsync()).PhysicalQuantity;
        var delivery = await fixture.Db.Deliveries.AsNoTracking().SingleAsync();

        var result = await fixture.Service.CorrectDateAsync(new(delivery.Id, delivery.Version, Actual, "Según boleta del 29/10"), fixture.UserId);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Equal(Actual, (await fixture.Db.Deliveries.AsNoTracking().SingleAsync()).CompletedAtUtc);
        var sale = await fixture.Db.Sales.AsNoTracking().SingleAsync();
        Assert.Equal(Actual, sale.OccurredAtUtc);
        Assert.Equal(150m, sale.Amount);
        Assert.Equal(10, (await fixture.Db.DeliveryLines.AsNoTracking().SingleAsync()).DeliveredQuantity);
        Assert.Equal(stockBefore, (await fixture.Db.ProductStockBalances.AsNoTracking().SingleAsync()).PhysicalQuantity);
        Assert.Equal(Now.AddDays(-2), (await fixture.Db.Payments.AsNoTracking().SingleAsync()).PaymentDateUtc);
        Assert.Equal(50m, (await fixture.Db.Receivables.AsNoTracking().SingleAsync()).PaidAmount);
        var account = Assert.Single(await fixture.Service.ReceivablesAsync());
        Assert.Equal(Actual, account.DeliveredAtUtc);
        Assert.Equal(100m, account.Balance);
        var audit = await fixture.Db.AuditEntries.SingleAsync(x => x.Action == "DeliveryDateCorrected");
        Assert.Contains("2026-11-20", audit.PreviousValuesJson);
        Assert.Contains("2026-10-29", audit.NewValuesJson);
        Assert.Equal(Now, audit.OccurredAtUtc);
        var analytics = new SalesAnalyticsService(fixture.Db);
        Assert.Equal(150m, (await analytics.GetAsync(new(2026, 10, 29), new(2026, 10, 29))).Revenue);
        Assert.Equal(0m, (await analytics.GetAsync(new(2026, 11, 20), new(2026, 11, 20))).Revenue);
        var income = new IncomeStatementService(fixture.Db, new FixedClock());
        Assert.Equal(150m, (await income.GetAsync(new(2026, 10, 29), new(2026, 10, 29))).SalesRevenue);
        Assert.Equal(0m, (await income.GetAsync(new(2026, 11, 20), new(2026, 11, 20))).SalesRevenue);
        Assert.Equal(50m, (await fixture.Service.GetBoardAsync(new(2026, 11, 18))).CollectedToday);
        Assert.Equal(0m, (await fixture.Service.GetBoardAsync(new(2026, 11, 20))).CollectedToday);
    }

    [Fact]
    public async Task MissingAndFutureDatesAreRejectedBeforeConsumingStock()
    {
        await using var fixture = await Fixture.CreateAsync();
        Assert.False((await fixture.CompleteAsync(default)).Succeeded);
        Assert.False((await fixture.CompleteAsync(Now.AddDays(1))).Succeeded);
        Assert.Empty(await fixture.Db.Sales.ToListAsync());
        Assert.Equal(20, (await fixture.Db.ProductStockBalances.SingleAsync()).PhysicalQuantity);
    }

    [Fact]
    public async Task CorrectionRequiresPermissionReasonAndCurrentVersion()
    {
        await using var fixture = await Fixture.CreateAsync();
        Assert.True((await fixture.CompleteAsync(Now)).Succeeded);
        var delivery = await fixture.Db.Deliveries.AsNoTracking().SingleAsync();
        Assert.False((await fixture.Service.CorrectDateAsync(new(delivery.Id, delivery.Version, Actual, "Boleta"), Guid.NewGuid())).Succeeded);
        Assert.False((await fixture.Service.CorrectDateAsync(new(delivery.Id, delivery.Version - 1, Actual, "Boleta"), fixture.UserId)).Succeeded);
        Assert.False((await fixture.Service.CorrectDateAsync(new(delivery.Id, delivery.Version, Actual, ""), fixture.UserId)).Succeeded);
        Assert.False((await fixture.Service.CorrectDateAsync(new(delivery.Id, delivery.Version, Now.AddDays(1), "Boleta"), fixture.UserId)).Succeeded);
        Assert.Equal(Now, (await fixture.Db.Deliveries.AsNoTracking().SingleAsync()).CompletedAtUtc);
        Assert.Empty(await fixture.Db.AuditEntries.Where(x => x.Action == "DeliveryDateCorrected").ToListAsync());
    }

    [Fact]
    public async Task CorrectionOfLastPartialDeliveryPreservesEarlierSaleDate()
    {
        await using var fixture = await Fixture.CreateAsync();
        var firstDate = DeliveryDates.ToUtc(new(2026, 10, 20));
        Assert.True((await fixture.Service.CompleteAsync(new(fixture.DeliveryId, 1, "part-one", "María", [new(fixture.DeliveryLineId, 4)], 0, null, null, false, firstDate), fixture.UserId)).Succeeded);
        var partial = await fixture.Db.Deliveries.AsNoTracking().SingleAsync();
        Assert.True((await fixture.Service.CompleteAsync(new(fixture.DeliveryId, partial.Version, "part-two", "María", [new(fixture.DeliveryLineId, 6)], 0, null, null, false, Now), fixture.UserId)).Succeeded);
        var completed = await fixture.Db.Deliveries.AsNoTracking().SingleAsync();
        var correction = await fixture.Service.CorrectDateAsync(new(completed.Id, completed.Version, Actual, "Boleta de la segunda entrega"), fixture.UserId);
        Assert.True(correction.Succeeded, string.Join(" ", correction.Errors));
        var sales = await fixture.Db.Sales.AsNoTracking().OrderBy(x => x.OccurredAtUtc).ToListAsync();
        Assert.Equal(2, sales.Count);
        Assert.Equal(firstDate, sales[0].OccurredAtUtc);
        Assert.Equal(60m, sales[0].Amount);
        Assert.Equal(Actual, sales[1].OccurredAtUtc);
        Assert.Equal(90m, sales[1].Amount);
    }

    [Fact]
    public void BusinessDateDoesNotDependOnServerTimezone()
    {
        Assert.Equal(new DateTimeOffset(2026, 10, 29, 4, 0, 0, TimeSpan.Zero), Actual);
        Assert.Equal(new DateTime(2026, 10, 29), DeliveryDates.LocalDate(Actual));
        Assert.Equal(new DateTime(2026, 10, 28), DeliveryDates.LocalDate(new(2026, 10, 29, 2, 0, 0, TimeSpan.Zero)));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public required ApplicationDbContext Db { get; init; }
        public required DeliveryService Service { get; init; }
        public Guid UserId { get; init; }
        public Guid OrderId { get; init; }
        public Guid DeliveryId { get; init; }
        public Guid DeliveryLineId { get; init; }

        public Task<DeliveryResult> CompleteAsync(DateTimeOffset at) => Service.CompleteAsync(new(DeliveryId, 1, "complete-test", "María", [new(DeliveryLineId, 10)], 0, null, null, false, at), UserId);

        public static async Task<Fixture> CreateAsync()
        {
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
            var user = Guid.NewGuid();
            var role = Guid.NewGuid();
            db.Users.Add(new ApplicationUser { Id = user, DisplayName = "Administración" });
            db.Roles.Add(new IdentityRole<Guid> { Id = role, Name = AppRoles.Administrator });
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user, RoleId = role });
            var customer = Customer.Create("Nutriarte", null, null, Now);
            var product = Product.Create("Budín proteico", "budin", "Unidad", Now);
            var order = Order.CreateDraft(customer.Id, null, user, Now);
            order.Configure(customer.Id, null, null, null, DeliveryDates.ToUtc(new(2026, 10, 20)), null, user, Now);
            var line = order.AddLine(product.Id, product.Name, 10, 15m, 15m, null, user, Now);
            order.Confirm(OrderSnapshot.Create(order.Id, customer.Name, null, null, null, null, null, Now), user, Now);
            order.AdvanceTo(OrderStatus.InPreparation, "Preparado", user, Now);
            order.AdvanceTo(OrderStatus.Ready, "Listo", user, Now);
            var stock = ProductStockBalance.Create(product.Id);
            stock.Produce(20);
            order.Reservations[0].Allocate(stock.Reserve(10));
            var delivery = Delivery.Create(order.Id, user, Now, [(line.Id, 10)], user, Now);
            db.Customers.Add(customer); db.Products.Add(product); db.Orders.Add(order); db.Deliveries.Add(delivery); db.ProductStockBalances.Add(stock);
            db.ProductionBatches.Add(ProductionBatch.Create("TEST", product.Id, null, Actual.AddDays(-1), 20, 0, 5m, user));
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            return new Fixture { Db = db, Service = new DeliveryService(db, new FixedClock(), Options.Create(new CloudinaryOptions()), null!, null!), UserId = user, OrderId = order.Id, DeliveryId = delivery.Id, DeliveryLineId = delivery.Lines[0].Id };
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
}
