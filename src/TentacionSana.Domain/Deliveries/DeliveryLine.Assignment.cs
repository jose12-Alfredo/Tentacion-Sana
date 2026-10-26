namespace TentacionSana.Domain.Deliveries;

public sealed partial class DeliveryLine
{
    public void SetPendingQuantities(int sale, int replacement, int tasting)
    {
        if (sale < 0 || replacement < 0 || tasting < 0 || sale + replacement + tasting > 10000)
            throw new ArgumentException("La cantidad pendiente ajustada no es válida.");

        AssignedSaleQuantity = DeliveredSaleQuantity + sale;
        AssignedReplacementQuantity = DeliveredReplacementQuantity + replacement;
        AssignedTastingQuantity = DeliveredTastingQuantity + tasting;
        AssignedQuantity = AssignedSaleQuantity + AssignedReplacementQuantity + AssignedTastingQuantity;
    }
}
