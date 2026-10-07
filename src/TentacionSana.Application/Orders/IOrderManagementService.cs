namespace TentacionSana.Application.Orders;

public interface IOrderManagementService
{
    Task<OrderOperationResult> CreateDraftAsync(Guid customerId, Guid userId, CancellationToken cancellationToken = default);
    Task<OrderOperationResult> ConfigureDraftAsync(Guid orderId, int expectedVersion, OrderConfiguration command, Guid userId, CancellationToken cancellationToken = default);
    Task<OrderOperationResult> AddLineAsync(Guid orderId, int expectedVersion, OrderLineCommand command, Guid userId, CancellationToken cancellationToken = default);
    Task<OrderOperationResult> RemoveLineAsync(Guid orderId, Guid lineId, int expectedVersion, Guid userId, CancellationToken cancellationToken = default);
    Task<OrderOperationResult> ConfirmAsync(Guid orderId, int expectedVersion, Guid userId, CancellationToken cancellationToken = default);
    Task<OrderOperationResult> RegisterCompletedOrderAsync(Guid orderId, int expectedVersion, OrderConfiguration configuration, DateTimeOffset completedAtUtc, string receiverName, Guid userId, CancellationToken cancellationToken = default);
    Task<OrderOperationResult> CancelAsync(Guid orderId, int expectedVersion, string reason, Guid userId, CancellationToken cancellationToken = default);
    Task<OrderOperationResult> DeleteDraftAsync(Guid orderId, int expectedVersion, Guid userId, CancellationToken cancellationToken = default);
    Task<OrderOperationResult> ArchiveAsync(Guid orderId, int expectedVersion, string reason, Guid userId, CancellationToken cancellationToken = default);
    Task<OrderOperationResult> ReviseConfigurationAsync(Guid orderId, int expectedVersion, OrderConfiguration command, string reason, Guid userId, CancellationToken cancellationToken = default);
    Task<OrderOperationResult> ReviseLineAsync(Guid orderId, Guid lineId, int expectedVersion, ConfirmedLineCommand command, Guid userId, CancellationToken cancellationToken = default);
    Task<OrderOperationResult> RemoveConfirmedLineAsync(Guid orderId, Guid lineId, int expectedVersion, string reason, Guid userId, CancellationToken cancellationToken = default);
    Task<OrderOperationResult> AdvanceStatusAsync(Guid orderId, int expectedVersion, string nextStatus, string reason, Guid userId, CancellationToken cancellationToken = default);
    Task<OrderDetail?> GetAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrderListItem>> ListAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrderOption>> CustomerOptionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductOption>> ProductOptionsAsync(CancellationToken cancellationToken = default);
}

public sealed record OrderConfiguration(Guid CustomerId, Guid? DeliveryPointId, Guid? ContactId, Guid? PaymentResponsiblePartyId, DateTimeOffset? PromisedAtUtc, string? Notes);
public sealed record OrderLineCommand(Guid ProductId, int SaleQuantity, int ReplacementQuantity, int TastingQuantity, decimal SoldUnitPrice, string? DiscountReason, string? ReplacementReason, string? ReplacementNotes, string? TastingReason = null, string? TastingNotes = null);
public sealed record ConfirmedLineCommand(int SaleQuantity, int ReplacementQuantity, int TastingQuantity, decimal SoldUnitPrice, string? DiscountReason, string? ReplacementReason, string? ReplacementNotes, string? TastingReason, string? TastingNotes, string Reason);
public sealed record OrderOperationResult(bool Succeeded, Guid? Id, IReadOnlyList<string> Errors);
public sealed record OrderListItem(Guid Id, long Number, string Customer, string? DeliveryPoint, string? PointImageUrl, string? ContactPhone, string? LocationUrl, string Status, string PaymentStatus, string? PaymentMethod, int TodayQrPayments, decimal TodayQrAmount, int TodayCashPayments, decimal TodayCashAmount, DateTimeOffset? PromisedAtUtc, int SaleQuantity, int ReplacementQuantity, int TastingQuantity, int TotalQuantity, int PendingSaleQuantity, int PendingReplacementQuantity, int PendingTastingQuantity, int PendingQuantity, string? Notes, decimal Total, decimal Paid, decimal Balance, DateTimeOffset CreatedAtUtc, int Version, IReadOnlyList<string> Products);
public sealed record OrderLineDetail(Guid Id, Guid ProductId, string Product, int SaleQuantity, int ReplacementQuantity, int TastingQuantity, int Quantity, decimal StandardUnitPrice, decimal SoldUnitPrice, decimal UnitDiscount, string? DiscountReason, string? ReplacementReason, string? ReplacementNotes, string? TastingReason, string? TastingNotes, int ReservedQuantity, int ShortageQuantity);
public sealed record OrderHistoryItem(DateTimeOffset AtUtc, string Kind, string Description);
public sealed record OrderDetail(Guid Id, long Number, Guid CustomerId, Guid? DeliveryPointId, Guid? ContactId, Guid? PaymentResponsiblePartyId, string Customer, string? DeliveryPoint, string? Contact, string? Driver, string? DeliveryStatus, string Status, string PaymentStatus, string? PaymentMethod, decimal Paid, decimal Balance, int ReceivableVersion, DateTimeOffset? PromisedAtUtc, string? Notes, int Version, IReadOnlyList<OrderLineDetail> Lines, IReadOnlyList<OrderHistoryItem> History, int ShortageQuantity);
public sealed record OrderOption(Guid Id, string Name, IReadOnlyList<OrderPointOption> DeliveryPoints, IReadOnlyList<OrderSubOption> Contacts, IReadOnlyList<OrderSubOption> Payers);
public sealed record OrderPointOption(Guid Id, string Name, string Address, string? Reference, string? Location, Guid? ContactId, Guid? PayerId);
public sealed record OrderSubOption(Guid Id, string Name, string? Phone = null);
public sealed record ProductOption(Guid Id, string Name, decimal CurrentPrice);
