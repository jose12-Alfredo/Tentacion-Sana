namespace TentacionSana.Domain.Catalog;

public sealed class Product
{
    private Product() { }

    private Product(Guid id, string name, string slug, string presentation, DateTimeOffset createdAtUtc)
    {
        Id = id;
        SetIdentity(name, slug, presentation);
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid? CategoryId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string Presentation { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Product Create(string name, string slug, string presentation, DateTimeOffset nowUtc) =>
        new(Guid.NewGuid(), name, slug, presentation, nowUtc);

    public void Update(string name, string slug, string presentation, Guid? categoryId, DateTimeOffset nowUtc)
    {
        SetIdentity(name, slug, presentation);
        CategoryId = categoryId;
        UpdatedAtUtc = nowUtc;
    }

    public void Deactivate(DateTimeOffset nowUtc)
    {
        IsActive = false;
        UpdatedAtUtc = nowUtc;
    }

    public void Activate(DateTimeOffset nowUtc)
    {
        IsActive = true;
        UpdatedAtUtc = nowUtc;
    }

    private void SetIdentity(string name, string slug, string presentation)
    {
        Name = RequireText(name, nameof(name), 160);
        Slug = RequireText(slug, nameof(slug), 180).ToLowerInvariant();
        Presentation = RequireText(presentation, nameof(presentation), 160);
    }

    private static string RequireText(string value, string parameterName, int maximumLength)
    {
        var normalized = value.Trim();
        if (normalized.Length is 0 || normalized.Length > maximumLength)
        {
            throw new ArgumentException($"{parameterName} debe contener entre 1 y {maximumLength} caracteres.", parameterName);
        }

        return normalized;
    }
}
