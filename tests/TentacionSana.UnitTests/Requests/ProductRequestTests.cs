using TentacionSana.Domain.Requests;

namespace TentacionSana.UnitTests.Requests;

public sealed class ProductRequestTests
{
    [Fact]
    public void CreateRequiresConsent() => Assert.Throws<ArgumentException>(() => ProductRequest.Create("JosÃ©", "789", null, null, null, "Web", false, DateTimeOffset.UtcNow));

    [Fact]
    public void AddLineRequiresPositiveQuantity()
    {
        var request = ProductRequest.Create("JosÃ©", "789", null, null, null, "Web", true, DateTimeOffset.UtcNow);
        Assert.Throws<ArgumentOutOfRangeException>(() => request.AddLine(Guid.NewGuid(), "BudÃ­n", 0));
    }

    [Fact]
    public void CreateAddsInitialStatusHistory()
    {
        var now = DateTimeOffset.UtcNow;
        var request = ProductRequest.Create("José", "789", null, null, null, "Web", true, now);

        var history = Assert.Single(request.StatusHistory);
        Assert.Null(history.PreviousStatus);
        Assert.Equal(RequestStatus.New, history.NewStatus);
        Assert.Equal(now, history.ChangedAtUtc);
    }

    [Fact]
    public void MarkContactedRequiresReasonAndStoresActor()
    {
        var request = ProductRequest.Create("José", "789", null, null, null, "Web", true, DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => request.MarkContacted(userId, " ", DateTimeOffset.UtcNow));

        request.MarkContacted(userId, "Se respondió por WhatsApp.", DateTimeOffset.UtcNow);
        var history = request.StatusHistory[^1];
        Assert.Equal(RequestStatus.Contacted, request.Status);
        Assert.Equal(userId, history.ChangedByUserId);
        Assert.Equal(RequestStatus.New, history.PreviousStatus);
    }

    [Fact]
    public void DeletingConvertedDraftReopensItsRequest()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var request = ProductRequest.Create("José", "789", null, null, null, "Web", true, now);
        request.MarkConverted(Guid.NewGuid(), orderId, userId, now);

        request.ReopenAfterOrderDeletion(orderId, userId, now.AddMinutes(1));

        Assert.Equal(RequestStatus.Contacted, request.Status);
        Assert.Null(request.ConvertedOrderId);
        Assert.Equal("Pedido borrador eliminado; solicitud reabierta.", request.StatusHistory[^1].Reason);
    }
}

