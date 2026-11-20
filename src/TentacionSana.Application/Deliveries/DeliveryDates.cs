namespace TentacionSana.Application.Deliveries;

public static class DeliveryDates
{
    // Santa Cruz, Bolivia: UTC-4, sin cambio de horario estacional.
    public static readonly TimeSpan BusinessOffset = TimeSpan.FromHours(-4);
    public static DateTime LocalDate(DateTimeOffset utc) => utc.ToOffset(BusinessOffset).Date;
    public static DateTimeOffset ToUtc(DateTime date) =>
        new DateTimeOffset(DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified), BusinessOffset).ToUniversalTime();
}
