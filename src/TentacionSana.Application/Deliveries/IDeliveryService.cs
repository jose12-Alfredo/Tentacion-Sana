namespace TentacionSana.Application.Deliveries;

public interface IDeliveryService
{
    Task<DeliveryResult> ScheduleAsync(ScheduleDeliveryCommand command, Guid userId, CancellationToken cancellationToken = default);
    Task<DeliveryResult> AssignAsync(Guid deliveryId, int expectedVersion, Guid driverUserId, string reason, Guid userId, CancellationToken cancellationToken = default);
    Task<DeliveryResult> StartAsync(Guid deliveryId, int expectedVersion, Guid userId, CancellationToken cancellationToken = default);
    Task<DeliveryResult> ReturnOrderToPendingAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default);
    Task<DeliveryResult> CompleteAsync(CompleteDeliveryCommand command, Guid userId, CancellationToken cancellationToken = default);
    Task<DeliveryResult> FailAsync(Guid deliveryId, int expectedVersion, string reason, Guid userId, CancellationToken cancellationToken = default);
    Task<DeliveryResult> PlanRouteAsync(PlanDeliveryRouteCommand command, Guid userId, CancellationToken cancellationToken = default);
    Task<DeliveryResult> StartRouteAsync(Guid routeId, int expectedVersion, Guid userId, CancellationToken cancellationToken = default);
    Task<DeliveryResult> ReturnRouteToPendingAsync(Guid routeId, int expectedVersion, Guid userId, CancellationToken cancellationToken = default);
    Task<DeliveryResult> ReorderRouteAsync(ReorderDeliveryRouteCommand command, Guid userId, CancellationToken cancellationToken = default);
    Task<DeliveryResult> RegisterPaymentAsync(RegisterPaymentCommand command, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeliveryItem>> ListAsync(Guid? assignedUserId = null, CancellationToken cancellationToken = default);
    Task<DeliveryDetail?> GetAsync(Guid deliveryId, Guid? assignedUserId = null, CancellationToken cancellationToken = default);
    Task<DeliveryBoard> GetBoardAsync(DateOnly referenceDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeliveryRouteListItem>> ListRoutesAsync(DateOnly promisedDate, Guid? driverUserId = null, CancellationToken cancellationToken = default);
    Task<DeliveryRouteDetail?> GetRouteAsync(Guid routeId, CancellationToken cancellationToken = default);
    Task<DeliveryRouteDetail?> GetCurrentRouteAsync(Guid driverUserId, CancellationToken cancellationToken = default);
    Task<DeliveryRouteDetail?> PrepareCurrentRouteAsync(Guid driverUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeliveryOrderOption>> SchedulableOrdersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DriverOption>> DriverOptionsAsync(Guid? includeCurrentUserId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReceivableItem>> ReceivablesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentHistoryItem>> PaymentsAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<SignedEvidenceResult> GetPaymentEvidenceUrlAsync(Guid evidenceId, Guid userId, CancellationToken cancellationToken = default);
}

public interface IDeliveryEvidenceService
{
    Task<EvidenceResult> UploadAsync(UploadEvidenceCommand command, Guid userId, CancellationToken cancellationToken = default);
    Task<SignedEvidenceResult> GetSignedUrlAsync(Guid evidenceId, Guid userId, CancellationToken cancellationToken = default);
}

public sealed record ScheduleDeliveryCommand(Guid OrderId, Guid? DriverUserId, DateTimeOffset ScheduledAtUtc, IReadOnlyList<ScheduleLine> Lines);
public sealed record ScheduleLine(Guid OrderLineId, int Quantity);
public sealed record CompleteDeliveryCommand(Guid DeliveryId, int ExpectedVersion, string IdempotencyKey, string ReceiverName, IReadOnlyList<CompleteLine> Lines, decimal PaymentAmount, string? PaymentMethod, string? ExternalReference, bool CollectedByDriver);
public sealed record CompleteLine(Guid DeliveryLineId, int SaleQuantity, int ReplacementQuantity, int TastingQuantity)
{
    public CompleteLine(Guid deliveryLineId, int quantity) : this(deliveryLineId, quantity, 0, 0) { }
    public int Quantity => SaleQuantity + ReplacementQuantity + TastingQuantity;
}
public sealed record PlanDeliveryRouteCommand(DateOnly PromisedDate, Guid DriverUserId, IReadOnlyList<Guid> OrderIds);
public sealed record ReorderDeliveryRouteCommand(Guid RouteId, int ExpectedVersion, IReadOnlyList<Guid> DeliveryIds);
public sealed record RegisterPaymentCommand(Guid OrderId, int ExpectedReceivableVersion, string IdempotencyKey, decimal Amount, string PaymentMethod, string? ExternalReference, bool CollectedByCurrentUser, bool IsAdvancePayment = false, DateTimeOffset? PaymentDateUtc = null, string? Notes = null, Stream? EvidenceContent = null, string? EvidenceFileName = null, string? EvidenceContentType = null, long EvidenceLength = 0, Guid? DeliveryId = null);
public sealed record UploadEvidenceCommand(Guid DeliveryId, Stream Content, string FileName, string ContentType, long Length);
public sealed record DeliveryResult(bool Succeeded, Guid? Id, IReadOnlyList<string> Errors);
public sealed record EvidenceResult(bool Succeeded, Guid? Id, IReadOnlyList<string> Errors);
public sealed record SignedEvidenceResult(bool Succeeded, string? Url, IReadOnlyList<string> Errors);
public sealed record DeliveryItem(Guid Id, long OrderNumber, string Customer, string Status, Guid? DriverUserId, string? DriverName, DateTimeOffset ScheduledAtUtc, int PendingQuantity, int Version, int? RoutePosition = null, decimal Total = 0, decimal Paid = 0, decimal Balance = 0, int DeliveryEvidenceCount = 0, int PaymentEvidenceCount = 0, string? PaymentMethod = null, int DeliveredQuantity = 0, string? DeliveryPoint = null, Guid? DeliveryPointId = null, string? Address = null, string? Reference = null, string? Location = null, double? Latitude = null, double? Longitude = null);
public sealed record DeliveryLineItem(Guid Id, Guid OrderLineId, string Product, int SaleQuantity, int ReplacementQuantity, int TastingQuantity, int AssignedQuantity, int DeliveredSaleQuantity, int DeliveredReplacementQuantity, int DeliveredTastingQuantity, int DeliveredQuantity, int PendingQuantity, int PendingSaleQuantity, int PendingReplacementQuantity, int PendingTastingQuantity);
public sealed record DeliveryDetail(Guid Id, Guid OrderId, long OrderNumber, string Customer, string? Address, string? Location, string? ContactName, string? ContactPhone, string? Notes, string Status, Guid? DriverUserId, string? DriverName, DateTimeOffset ScheduledAtUtc, int Version, IReadOnlyList<DeliveryLineItem> Lines, IReadOnlyList<EvidenceItem> Evidence, IReadOnlyList<DeliveryHistoryItem> History, decimal Balance, int ReceivableVersion, decimal Total = 0, decimal Paid = 0, IReadOnlyList<PaymentHistoryItem>? Payments = null);
public sealed record EvidenceItem(Guid Id, string FileName, DateTimeOffset UploadedAtUtc);
public sealed record DeliveryHistoryItem(DateTimeOffset AtUtc, string Status, string Reason);
public sealed record DeliveryOrderLineOption(Guid Id, string Product, int SaleAvailable, int ReplacementAvailable, int TastingAvailable, int AvailableToSchedule);
public sealed record DeliveryOrderOption(Guid Id, long Number, string Customer, DateTimeOffset? PromisedAtUtc, IReadOnlyList<DeliveryOrderLineOption> Lines);
public sealed record DriverOption(Guid Id, string Name);
public sealed record DeliveryBoard(DateOnly ReferenceDate, IReadOnlyList<DeliveryBoardItem> Items, int DeliveredTodayCount, decimal CollectedToday);
public sealed record DeliveryBoardItem(Guid OrderId, Guid? DeliveryId, Guid? RouteId, int? RoutePosition, long OrderNumber, string Customer, string? DeliveryPoint, Guid? DeliveryPointId, string? Address, string? Reference, string? Location, double? Latitude, double? Longitude, string? ContactName, string? ContactPhone, int SalePending, int ReplacementPending, int TastingPending, decimal Balance, string Status, Guid? DriverUserId, string? DriverName, int DeliveryVersion, bool CanAddToRoute, bool HasLocation, DateTimeOffset? CompletedAtUtc, DateTimeOffset? PromisedAtUtc = null, decimal Total = 0, decimal Paid = 0);
public sealed record DeliveryRouteListItem(Guid Id, Guid DriverUserId, string DriverName, string Status, int StopCount, DateTimeOffset CreatedAtUtc, DateTimeOffset? StartedAtUtc);
public sealed record DeliveryRouteDetail(Guid Id, Guid DriverUserId, string DriverName, string Status, int Version, DateTimeOffset? StartedAtUtc, IReadOnlyList<DeliveryRouteStopItem> Stops);
public sealed record DeliveryRouteStopItem(Guid StopId, Guid DeliveryId, long OrderNumber, string Customer, string? DeliveryPoint, Guid? DeliveryPointId, string? Address, string? Reference, string? Location, double? Latitude, double? Longitude, int SalePending, int ReplacementPending, int TastingPending, decimal Balance, string Status, int Position);
public sealed record ReceivableItem(Guid OrderId, long OrderNumber, string Customer, string Payer, decimal Invoiced, decimal Paid, decimal Balance, int Version, DateTimeOffset? DeliveredAtUtc);
public sealed record PaymentHistoryItem(Guid Id, decimal Amount, string Method, DateTimeOffset PaymentDateUtc, string? Notes, Guid EvidenceId);
