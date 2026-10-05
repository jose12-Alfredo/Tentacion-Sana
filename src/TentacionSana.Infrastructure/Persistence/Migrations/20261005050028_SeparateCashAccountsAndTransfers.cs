using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TentacionSana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeparateCashAccountsAndTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                schema: "tentacion_sana",
                table: "CashMovements",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE tentacion_sana."CashMovements"
                SET "Category" = CASE
                    WHEN "Source" = 'CustomerPayment' THEN 'Cobro pedido'
                    WHEN "Source" = 'InventoryPurchase' THEN 'Ingredientes'
                    ELSE 'Otros'
                END
                WHERE "Category" = '';
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "TransferId",
                schema: "tentacion_sana",
                table: "CashMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CashCounts",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CountedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpectedAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    CountedAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Difference = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Observation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EvidencePublicId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    EvidenceFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    EvidenceFormat = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EvidenceBytes = table.Column<long>(type: "bigint", nullable: false),
                    RegisteredByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashCounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_TransferId",
                schema: "tentacion_sana",
                table: "CashMovements",
                column: "TransferId");

            migrationBuilder.CreateIndex(
                name: "IX_CashCounts_CountedAtUtc",
                schema: "tentacion_sana",
                table: "CashCounts",
                column: "CountedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashCounts",
                schema: "tentacion_sana");

            migrationBuilder.DropIndex(
                name: "IX_CashMovements_TransferId",
                schema: "tentacion_sana",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "Category",
                schema: "tentacion_sana",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "TransferId",
                schema: "tentacion_sana",
                table: "CashMovements");
        }
    }
}
