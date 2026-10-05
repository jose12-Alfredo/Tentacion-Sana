using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TentacionSana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillCashLedgerFromPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO tentacion_sana."CashMovements"
                ("Id","Account","Direction","Source","OccurredAtUtc","Detail","Amount","PaymentId","SupplyPurchaseId",
                 "EvidencePublicId","EvidenceFileName","EvidenceFormat","EvidenceBytes","RegisteredByUserId","RegisteredAtUtc")
                SELECT payment."Id",
                       CASE WHEN payment."Method" = 'Qr' THEN 'Bank' ELSE 'Cash' END,
                       'Income','CustomerPayment',payment."PaymentDateUtc",
                       'Pago de cliente del pedido ' || payment."OrderId",payment."Amount",payment."Id",NULL,
                       evidence."PublicId",evidence."FileName",evidence."Format",evidence."Bytes",
                       payment."ReceivedByUserId",payment."ReceivedAtUtc"
                FROM tentacion_sana."Payments" payment
                JOIN tentacion_sana."PaymentEvidence" evidence ON evidence."PaymentId" = payment."Id"
                WHERE payment."Status" = 'Confirmed'
                  AND payment."Method" IN ('Qr','Cash')
                  AND NOT EXISTS (SELECT 1 FROM tentacion_sana."CashMovements" movement WHERE movement."PaymentId" = payment."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM tentacion_sana.\"CashMovements\" WHERE \"Source\" = 'CustomerPayment';");
        }
    }
}
