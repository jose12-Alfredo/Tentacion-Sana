using TentacionSana.Application.Orders;

namespace TentacionSana.UnitTests.Orders;

public sealed class DeliveryNotePaymentProjectionTests
{
    [Theory]
    [InlineData(false, 265, 265, 0, true, null)]
    [InlineData(true, 265, 265, 0, true, "PENDIENTE DE PAGO")]
    [InlineData(true, 265, 265, 100, true, "PAGO PARCIAL")]
    [InlineData(true, 265, 265, 265, true, null)]
    [InlineData(true, 265, 265, 300, true, null)]
    [InlineData(true, 0, 0, 0, true, null)]
    [InlineData(true, 51, 265, 0, false, "PENDIENTE DE PAGO")]
    [InlineData(true, 51, 265, 265, false, null)]
    public void UsesOnlyVerifiablePaymentStates(bool show, decimal total, decimal orderTotal, decimal paid, bool whole, string? expected)
    {
        var result = DeliveryNotePaymentProjection.Evaluate(show, total, orderTotal, paid, whole, paid);
        Assert.Equal(expected, result.Label); Assert.Null(result.Warning);
    }

    [Fact]
    public void PartialDeliveryWithOrderLevelPaymentsHasNoInventedState()
    {
        var result = DeliveryNotePaymentProjection.Evaluate(true, 51, 265, 100, false, 100);
        Assert.Null(result.Label); Assert.Contains("entrega parcial", result.Warning);
    }

    [Fact]
    public void InconsistentReceivableAndAllocationsAreNotPresentedAsDebt()
    {
        var result = DeliveryNotePaymentProjection.Evaluate(true, 265, 265, 100, true, 0);
        Assert.Null(result.Label); Assert.NotNull(result.Warning);
    }
}
