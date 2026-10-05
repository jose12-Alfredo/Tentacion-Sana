using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TentacionSana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCashLedgerAndRestorePurchasePresentation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PurchasePresentation",
                schema: "tentacion_sana",
                table: "SupplyPurchases",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE tentacion_sana."SupplyPurchases" AS purchase
                SET "PurchasePresentation" = supply."PurchasePresentation"
                FROM tentacion_sana."Supplies" AS supply
                WHERE purchase."SupplyId" = supply."Id"
                  AND purchase."PurchasePresentation" = '';
                """);

            migrationBuilder.CreateTable(
                name: "CashMovements",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Account = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplyPurchaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidencePublicId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    EvidenceFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    EvidenceFormat = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EvidenceBytes = table.Column<long>(type: "bigint", nullable: false),
                    RegisteredByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisteredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashMovements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CashPayables",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplyPurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EvidencePublicId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    EvidenceFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    EvidenceFormat = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EvidenceBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashPayables", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_Account_OccurredAtUtc",
                schema: "tentacion_sana",
                table: "CashMovements",
                columns: new[] { "Account", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_PaymentId",
                schema: "tentacion_sana",
                table: "CashMovements",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_SupplyPurchaseId",
                schema: "tentacion_sana",
                table: "CashMovements",
                column: "SupplyPurchaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashPayables_SupplyPurchaseId",
                schema: "tentacion_sana",
                table: "CashPayables",
                column: "SupplyPurchaseId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashMovements",
                schema: "tentacion_sana");

            migrationBuilder.DropTable(
                name: "CashPayables",
                schema: "tentacion_sana");

            migrationBuilder.DropColumn(
                name: "PurchasePresentation",
                schema: "tentacion_sana",
                table: "SupplyPurchases");
        }
    }
}
