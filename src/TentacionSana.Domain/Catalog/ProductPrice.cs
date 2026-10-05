namespace TentacionSana.Domain.Catalog;

public sealed class ProductPrice
{
    private ProductPrice() { }

    public ProductPrice(Guid id, Guid productId, decimal amount, DateTimeOffset effectiveFromUtc)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "El precio no puede ser negativo.");
        }

        Id = id;
        ProductId = productId;
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        EffectiveFromUtc = effectiveFromUtc;
    }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTimeOffset EffectiveFromUtc { get; private set; }
    public DateTimeOffset? EffectiveToUtc { get; private set; }

    public void Close(DateTimeOffset effectiveToUtc)
    {
        if (effectiveToUtc <= EffectiveFromUtc)
        {
            throw new InvalidOperationException("La vigencia final debe ser posterior a la inicial.");
        }

        EffectiveToUtc = effectiveToUtc;
    }
}
