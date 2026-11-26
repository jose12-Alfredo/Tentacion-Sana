using TentacionSana.Application.Orders;

namespace TentacionSana.UnitTests.Orders;

public sealed class DeliveryNoteProjectionTests
{
    [Fact]
    public void RejectsInconsistentAccumulatedQuantitiesInsteadOfInventingRemainingUnits()
    {
        var line = new NoteOrderLineSource(Guid.NewGuid(), "Producto", 2, 0, 0, 17);
        var delivery = new NoteDeliverySource(Guid.NewGuid(), "Delivered", 2, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null,
            [new(line.Id, 3, 0, 0, 3, 0, 0)]);
        Assert.Throws<ArgumentException>(() => DeliveryNoteProjection.SelectLines([line], [delivery], delivery.Id, true));
        Assert.Throws<ArgumentException>(() => DeliveryNoteProjection.SelectLines([line], [delivery], null, false));
    }
    [Theory]
    [InlineData(15, 0, 0, 255, 15)]
    [InlineData(15, 0, 2, 255, 17)]
    [InlineData(15, 1, 0, 255, 16)]
    [InlineData(15, 1, 2, 255, 18)]
    [InlineData(0, 1, 2, 0, 3)]
    public void KeepsPhysicalAndCommercialQuantitiesSeparate(int sale, int replacement, int tasting, decimal subtotal, int physical)
    {
        var lines = DeliveryNoteProjection.SelectLines([new(Guid.NewGuid(), "Budín histórico", sale, replacement, tasting, 17,
            "Acuerdo comercial", "Reposición autorizada", "Promoción", "Para conocer el producto")], [], null, false);
        Assert.Equal(subtotal, Assert.Single(lines).LineTotal);
        Assert.Equal(physical, lines[0].PhysicalQuantity);
    }
    [Fact]
    public void PreservesRepeatedProductWithDifferentReasonsAndSpecialPrices()
    {
        var a = new NoteOrderLineSource(Guid.NewGuid(), "Mismo producto", 2, 0, 1, 12.50m, TastingReason: "Evento");
        var b = new NoteOrderLineSource(Guid.NewGuid(), "Mismo producto", 0, 0, 1, 20, TastingReason: "Promoción");
        var result = DeliveryNoteProjection.SelectLines([a, b], [], null, false);
        Assert.Equal(2, result.Count); Assert.Equal(25, result.Sum(x => x.LineTotal));
        Assert.Equal(["Evento", "Promoción"], result.Select(x => x.TastingReason));
    }
    [Fact]
    public void PartialDeliveryExcludesPreviouslyDeliveredUnitsAndRequiresSelection()
    {
        var line = new NoteOrderLineSource(Guid.NewGuid(), "Producto", 10, 2, 1, 17);
        var first = new NoteDeliverySource(Guid.NewGuid(), "Partial", 3, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, "Receptor",
            [new(line.Id, 5, 1, 1, 3, 1, 0)]);
        var second = new NoteDeliverySource(Guid.NewGuid(), "Assigned", 1, DateTimeOffset.UtcNow, null, null, null,
            [new(line.Id, 5, 1, 0, 0, 0, 0)]);
        Assert.Throws<ArgumentException>(() => DeliveryNoteProjection.SelectLines([line], [first, second], null, false));
        var result = Assert.Single(DeliveryNoteProjection.SelectLines([line], [first, second], first.Id, false));
        Assert.Equal(2, result.SaleQuantity); Assert.Equal(0, result.ReplacementQuantity); Assert.Equal(1, result.TastingQuantity);
        Assert.Equal(34, result.LineTotal);
        Assert.Equal(0, DeliveryNoteProjection.DeliveryFee(10, [first, second], first.Id, false));
    }
    [Fact]
    public void OnlyFirstPlannedDeliveryCarriesShippingAndSummaryUsesVerifiedAccumulatedUnits()
    {
        var line = new NoteOrderLineSource(Guid.NewGuid(), "Producto", 10, 0, 0, 17);
        var first = new NoteDeliverySource(Guid.NewGuid(), "Assigned", 1, DateTimeOffset.UtcNow.AddDays(-1), null, null, null, [new(line.Id, 3, 0, 0, 0, 0, 0)]);
        var second = new NoteDeliverySource(Guid.NewGuid(), "Assigned", 1, DateTimeOffset.UtcNow, null, null, null, [new(line.Id, 7, 0, 0, 0, 0, 0)]);
        Assert.Equal(10, DeliveryNoteProjection.DeliveryFee(10, [second, first], first.Id, false));
        Assert.Equal(0, DeliveryNoteProjection.DeliveryFee(10, [second, first], second.Id, false));
        var completed = first with { Status = "Delivered", CompletedAtUtc = DateTimeOffset.UtcNow, Lines = [new(line.Id, 3, 0, 0, 3, 0, 0)] };
        Assert.Equal(3, Assert.Single(DeliveryNoteProjection.SelectLines([line], [completed], completed.Id, true)).SaleQuantity);
        Assert.Throws<ArgumentException>(() => DeliveryNoteProjection.SelectLines([line], [completed], completed.Id, false));
    }
    [Fact]
    public void PendingUnassignedOrderSubtractsAllEarlierDeliveries()
    {
        var line = new NoteOrderLineSource(Guid.NewGuid(), "Producto", 10, 1, 2, 17);
        var completed = new NoteDeliverySource(Guid.NewGuid(), "Delivered", 2, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, "Receptor", [new(line.Id, 4, 1, 1, 4, 1, 1)]);
        var result = Assert.Single(DeliveryNoteProjection.SelectLines([line], [completed], null, false));
        Assert.Equal(6, result.SaleQuantity); Assert.Equal(0, result.ReplacementQuantity); Assert.Equal(1, result.TastingQuantity);
        Assert.Equal(0, DeliveryNoteProjection.DeliveryFee(10, [completed], null, false));
    }
}
