namespace TentacionSana.Domain.Deliveries;

public enum AccountStatementReportType { Current, History }

public sealed class AccountStatementReport
{
    private AccountStatementReport() { }
    public Guid Id { get; private set; }
    public string ReportNumber { get; private set; } = string.Empty;
    public Guid ResponsiblePartyId { get; private set; }
    public DateTimeOffset GeneratedAtUtc { get; private set; }
    public Guid GeneratedByUserId { get; private set; }
    public AccountStatementReportType Type { get; private set; }
    public decimal BalanceAtGeneration { get; private set; }
    public decimal? PreviousBalance { get; private set; }
    public decimal? RegisteredPayment { get; private set; }
    public bool IncludedDeliveryEvidence { get; private set; }
    public bool IncludedPaymentEvidence { get; private set; }
    public bool IncludedPartialPayments { get; private set; }
    public List<AccountStatementReportOrder> Orders { get; private set; } = [];

    public static AccountStatementReport Create(
        string reportNumber,
        Guid responsiblePartyId,
        Guid generatedByUserId,
        AccountStatementReportType type,
        decimal balance,
        decimal? previousBalance,
        decimal? registeredPayment,
        bool includeDeliveryEvidence,
        bool includePaymentEvidence,
        bool includePartialPayments,
        DateTimeOffset now,
        IEnumerable<(Guid OrderId, decimal Total, decimal Paid, decimal Balance)> orders)
    {
        if (string.IsNullOrWhiteSpace(reportNumber)) throw new ArgumentException("El identificador del reporte es obligatorio.");
        var report = new AccountStatementReport
        {
            Id = Guid.NewGuid(), ReportNumber = reportNumber.Trim(), ResponsiblePartyId = responsiblePartyId,
            GeneratedByUserId = generatedByUserId, Type = type, BalanceAtGeneration = decimal.Round(balance, 2),
            PreviousBalance = previousBalance is null ? null : decimal.Round(previousBalance.Value, 2),
            RegisteredPayment = registeredPayment is null ? null : decimal.Round(registeredPayment.Value, 2),
            IncludedDeliveryEvidence = includeDeliveryEvidence, IncludedPaymentEvidence = includePaymentEvidence,
            IncludedPartialPayments = includePartialPayments, GeneratedAtUtc = now
        };
        var position = 1;
        foreach (var order in orders)
            report.Orders.Add(AccountStatementReportOrder.Create(report.Id, order.OrderId, position++, order.Total, order.Paid, order.Balance));
        if (report.Orders.Count == 0) throw new ArgumentException("El reporte debe incluir al menos un pedido.");
        return report;
    }
}

public sealed class AccountStatementReportOrder
{
    private AccountStatementReportOrder() { }
    public Guid Id { get; private set; }
    public Guid ReportId { get; private set; }
    public Guid OrderId { get; private set; }
    public int Position { get; private set; }
    public decimal Total { get; private set; }
    public decimal Paid { get; private set; }
    public decimal Balance { get; private set; }

    internal static AccountStatementReportOrder Create(Guid reportId, Guid orderId, int position, decimal total, decimal paid, decimal balance) =>
        new() { Id = Guid.NewGuid(), ReportId = reportId, OrderId = orderId, Position = position, Total = decimal.Round(total, 2), Paid = decimal.Round(paid, 2), Balance = decimal.Round(balance, 2) };
}
