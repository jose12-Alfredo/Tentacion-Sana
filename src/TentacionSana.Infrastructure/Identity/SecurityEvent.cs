namespace TentacionSana.Infrastructure.Identity;

public sealed class SecurityEvent
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public bool Succeeded { get; set; }
    public string? IpAddress { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
}
