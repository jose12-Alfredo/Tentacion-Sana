namespace TentacionSana.Infrastructure.Auditing;

public sealed class AuditEntry
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? PreviousValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
}
