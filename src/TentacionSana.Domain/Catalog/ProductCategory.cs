namespace TentacionSana.Domain.Catalog;

public sealed class ProductCategory
{
    private ProductCategory() { }

    public ProductCategory(Guid id, string name, string slug, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(slug))
        {
            throw new ArgumentException("La categoría requiere nombre y slug.");
        }

        Id = id;
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        DisplayOrder = displayOrder;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; }
}
