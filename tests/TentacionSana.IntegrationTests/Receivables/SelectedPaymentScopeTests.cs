using Microsoft.Extensions.Options;
using TentacionSana.Application.Receivables;
using TentacionSana.Infrastructure.Media;
using TentacionSana.Infrastructure.Receivables;
using TentacionSana.IntegrationTests.Deliveries;

namespace TentacionSana.IntegrationTests.Receivables;

public sealed class SelectedPaymentScopeTests
{
    [Theory]
    [InlineData("empty")]
    [InlineData("foreign")]
    [InlineData("outside")]
    public async Task RejectsInvalidScopeBeforeUploadingEvidenceOrRecordingPayment(string scenario)
    {
        await using var fixture = await DeliveryDateTests.Fixture.CreateAsync();
        Assert.True((await fixture.CompleteAsync(new DateTimeOffset(2026, 10, 29, 4, 0, 0, TimeSpan.Zero))).Succeeded);
        var service = new ReceivablesService(fixture.Db, TimeProvider.System, Options.Create(new CloudinaryOptions()), null!, null!, null!);
        var account = Assert.Single(fixture.Db.Receivables);
        using var evidence = new MemoryStream([1, 2, 3]);
        IReadOnlyList<Guid> ids = scenario == "empty" ? [] : scenario == "foreign" ? [Guid.NewGuid()] : [fixture.OrderId];
        var result = await service.RegisterPaymentAsync(new(account.PayerId ?? account.CustomerId, Guid.NewGuid().ToString(), 10, "Qr",
            new DateTimeOffset(2026, 10, 30, 4, 0, 0, TimeSpan.Zero), null, false, scenario != "outside",
            scenario == "outside" ? [new(Guid.NewGuid(), 10)] : [], evidence, "pago.jpg", "image/jpeg", 3, ids), fixture.UserId);
        Assert.False(result.Succeeded);
        Assert.DoesNotContain(result.Errors, x => x.Contains("Cloudinary"));
        Assert.Empty(fixture.Db.Payments);
        Assert.Equal(150m, Assert.Single(fixture.Db.Receivables).Balance);
    }
}
