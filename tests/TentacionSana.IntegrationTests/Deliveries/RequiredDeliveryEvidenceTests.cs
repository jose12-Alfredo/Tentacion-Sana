using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using TentacionSana.Application.Deliveries;
using TentacionSana.Application.Orders;
using TentacionSana.Application.Security;
using TentacionSana.Domain.Deliveries;
using TentacionSana.Domain.Orders;
using TentacionSana.Infrastructure.Identity;
using TentacionSana.Infrastructure.Orders;
using TentacionSana.Infrastructure.Persistence;

namespace TentacionSana.IntegrationTests.Deliveries;

public sealed class RequiredDeliveryEvidenceTests
{
    [Fact]
    public async Task HistoricalOrderRegistersReceiptTogetherWithDeliveryAndSale()
    {
        await using var fixture = await DeliveryDateTests.Fixture.CreateAsync();
        var role = await fixture.Db.Roles.SingleAsync();
        role.NormalizedName = AppRoles.Administrator.ToUpperInvariant();
        var customer = await fixture.Db.Customers.SingleAsync();
        var product = await fixture.Db.Products.SingleAsync();
        var now = DateTimeOffset.UtcNow;
        var draft = Order.CreateDraft(customer.Id, null, fixture.UserId, now);
        draft.AddLine(product.Id, product.Name, 2, 15m, 15m, null, fixture.UserId, now);
        fixture.Db.Orders.Add(draft);
        await fixture.Db.SaveChangesAsync();
        var services = new ServiceCollection().AddLogging().AddSingleton(fixture.Db);
        services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<ApplicationDbContext>();
        await using var provider = services.BuildServiceProvider();
        var service = new OrderManagementService(fixture.Db, TimeProvider.System, provider.GetRequiredService<UserManager<ApplicationUser>>(), null!, new SavedReceipt(fixture.Db));
        using var stream = new MemoryStream([1, 2, 3]);
        var actual = now.AddDays(-20);
        var result = await service.RegisterCompletedOrderAsync(draft.Id, draft.Version,
            new(customer.Id, null, null, null, actual, null), actual, "María", fixture.UserId,
            new(stream, "boleta.jpg", "image/jpeg", 3));
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        fixture.Db.ChangeTracker.Clear();
        var delivery = await fixture.Db.Deliveries.SingleAsync(x => x.OrderId == draft.Id);
        Assert.Equal(actual, delivery.CompletedAtUtc);
        Assert.Single(await fixture.Db.DeliveryEvidence.Where(x => x.DeliveryId == delivery.Id).ToListAsync());
        Assert.Equal(30m, (await fixture.Db.Sales.SingleAsync(x => x.OrderId == draft.Id)).Amount);
        Assert.Equal(18, (await fixture.Db.ProductStockBalances.SingleAsync()).PhysicalQuantity);
    }

    private sealed class SavedReceipt(ApplicationDbContext db) : IDeliveryEvidenceService
    {
        public async Task<EvidenceResult> UploadAsync(UploadEvidenceCommand command, Guid userId, CancellationToken cancellationToken = default)
        {
            var evidence = DeliveryEvidence.Create(command.DeliveryId, "tests/historical-receipt", command.FileName, "jpg", command.Length, userId, DateTimeOffset.UtcNow);
            db.DeliveryEvidence.Add(evidence);
            await db.SaveChangesAsync(cancellationToken);
            return new(true, evidence.Id, []);
        }
        public Task<SignedEvidenceResult> GetSignedUrlAsync(Guid evidenceId, Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EvidenceContentResult> GetContentAsync(Guid evidenceId, Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("inactive")]
    [InlineData("other-delivery")]
    public async Task DeliveryWithoutReceiptCannotConsumeInventoryOrCreateSale(string scenario)
    {
        await using var fixture = await DeliveryDateTests.Fixture.CreateAsync();
        var receipt = await fixture.Db.DeliveryEvidence.SingleAsync();
        if (scenario == "inactive") fixture.Db.Entry(receipt).Property(x => x.IsActive).CurrentValue = false;
        else if (scenario == "other-delivery") fixture.Db.Entry(receipt).Property(x => x.DeliveryId).CurrentValue = Guid.NewGuid();
        else fixture.Db.DeliveryEvidence.Remove(receipt);
        await fixture.Db.SaveChangesAsync();
        var result = await fixture.CompleteAsync(new(2026, 10, 29, 4, 0, 0, TimeSpan.Zero));
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, x => x.Contains("boleta"));
        Assert.Equal(20, (await fixture.Db.ProductStockBalances.AsNoTracking().SingleAsync()).PhysicalQuantity);
        Assert.Equal(0, (await fixture.Db.DeliveryLines.AsNoTracking().SingleAsync()).DeliveredQuantity);
        Assert.Empty(await fixture.Db.Sales.ToListAsync());
        Assert.Empty(await fixture.Db.Receivables.ToListAsync());
    }

    [Fact]
    public async Task HistoricalOrderRequiresReceiptBeforeMakingChanges()
    {
        await using var fixture = await DeliveryDateTests.Fixture.CreateAsync();
        var customer = await fixture.Db.Customers.SingleAsync();
        var draft = Order.CreateDraft(customer.Id, null, fixture.UserId, DateTimeOffset.UtcNow);
        fixture.Db.Orders.Add(draft);
        await fixture.Db.SaveChangesAsync();
        var service = new OrderManagementService(fixture.Db, TimeProvider.System, null!, null!);
        var result = await service.RegisterCompletedOrderAsync(draft.Id, draft.Version,
            new(customer.Id, null, null, null, null, null), DateTimeOffset.UtcNow.AddDays(-20), "María", fixture.UserId);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, x => x.Contains("boleta"));
        Assert.Equal(OrderStatus.Draft, (await fixture.Db.Orders.AsNoTracking().SingleAsync(x => x.Id == draft.Id)).Status);
        Assert.Equal(20, (await fixture.Db.ProductStockBalances.AsNoTracking().SingleAsync()).PhysicalQuantity);
    }
}
