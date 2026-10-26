namespace TentacionSana.Domain.Deliveries;

public sealed partial class DeliveryLine
{
    public void ReducePendingTo(int sale, int replacement, int tasting)
    {
        if (sale < 0 || replacement < 0 || tasting < 0 ||
            sale > PendingSaleQuantity || replacement > PendingReplacementQuantity || tasting > PendingTastingQuantity)
            throw new ArgumentException("La cantidad pendiente ajustada no es válida.");

        AssignedSaleQuantity = DeliveredSaleQuantity + sale;
        AssignedReplacementQuantity = DeliveredReplacementQuantity + replacement;
        AssignedTastingQuantity = DeliveredTastingQuantity + tasting;
        AssignedQuantity = AssignedSaleQuantity + AssignedReplacementQuantity + AssignedTastingQuantity;
    }
}
