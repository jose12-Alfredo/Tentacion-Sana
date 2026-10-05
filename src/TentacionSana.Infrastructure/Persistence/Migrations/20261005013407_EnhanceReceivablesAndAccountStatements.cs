using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TentacionSana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceReceivablesAndAccountStatements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ResponsiblePartyId",
                schema: "tentacion_sana",
                table: "Payments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "AccountStatementReports",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ResponsiblePartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    GeneratedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GeneratedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BalanceAtGeneration = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    PreviousBalance = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    RegisteredPayment = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    IncludedDeliveryEvidence = table.Column<bool>(type: "boolean", nullable: false),
                    IncludedPaymentEvidence = table.Column<bool>(type: "boolean", nullable: false),
                    IncludedPartialPayments = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountStatementReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentAllocations",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    ApplicationOrder = table.Column<int>(type: "integer", nullable: false),
                    AppliedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "tentacion_sana",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "tentacion_sana",
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountStatementReportOrders",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Paid = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountStatementReportOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountStatementReportOrders_AccountStatementReports_Report~",
                        column: x => x.ReportId,
                        principalSchema: "tentacion_sana",
                        principalTable: "AccountStatementReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountStatementReportOrders_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "tentacion_sana",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                UPDATE tentacion_sana."Payments" AS payment
                SET "ResponsiblePartyId" = COALESCE(receivable."PayerId", receivable."CustomerId", orders."CustomerId")
                FROM tentacion_sana."Orders" AS orders
                LEFT JOIN tentacion_sana."Receivables" AS receivable ON receivable."OrderId" = orders."Id"
                WHERE payment."OrderId" = orders."Id";

                INSERT INTO tentacion_sana."PaymentAllocations"
                    ("Id", "PaymentId", "OrderId", "Amount", "ApplicationOrder", "AppliedAtUtc")
                SELECT payment."Id", payment."Id", payment."OrderId", payment."Amount", 1, payment."ReceivedAtUtc"
                FROM tentacion_sana."Payments" AS payment;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ResponsiblePartyId_PaymentDateUtc",
                schema: "tentacion_sana",
                table: "Payments",
                columns: new[] { "ResponsiblePartyId", "PaymentDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountStatementReportOrders_OrderId",
                schema: "tentacion_sana",
                table: "AccountStatementReportOrders",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountStatementReportOrders_ReportId_Position",
                schema: "tentacion_sana",
                table: "AccountStatementReportOrders",
                columns: new[] { "ReportId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountStatementReports_ReportNumber",
                schema: "tentacion_sana",
                table: "AccountStatementReports",
                column: "ReportNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountStatementReports_ResponsiblePartyId_GeneratedAtUtc",
                schema: "tentacion_sana",
                table: "AccountStatementReports",
                columns: new[] { "ResponsiblePartyId", "GeneratedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_OrderId_AppliedAtUtc",
                schema: "tentacion_sana",
                table: "PaymentAllocations",
                columns: new[] { "OrderId", "AppliedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_PaymentId_OrderId",
                schema: "tentacion_sana",
                table: "PaymentAllocations",
                columns: new[] { "PaymentId", "OrderId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountStatementReportOrders",
                schema: "tentacion_sana");

            migrationBuilder.DropTable(
                name: "PaymentAllocations",
                schema: "tentacion_sana");

            migrationBuilder.DropTable(
                name: "AccountStatementReports",
                schema: "tentacion_sana");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ResponsiblePartyId_PaymentDateUtc",
                schema: "tentacion_sana",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ResponsiblePartyId",
                schema: "tentacion_sana",
                table: "Payments");
        }
    }
}
