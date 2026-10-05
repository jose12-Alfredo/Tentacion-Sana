namespace TentacionSana.Infrastructure.Idempotency;

public sealed class IdempotencyRecord
{
    public Guid Id { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string? ResponseJson { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
