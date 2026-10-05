using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TentacionSana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SplitOrderSaleReplacementAndPaymentEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                schema: "tentacion_sana",
                table: "Payments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaymentDateUtc",
                schema: "tentacion_sana",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "DeliveryLocation",
                schema: "tentacion_sana",
                table: "OrderSnapshots",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryReference",
                schema: "tentacion_sana",
                table: "OrderSnapshots",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinesJson",
                schema: "tentacion_sana",
                table: "OrderSnapshots",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplacementNotes",
                schema: "tentacion_sana",
                table: "OrderLines",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReplacementQuantity",
                schema: "tentacion_sana",
                table: "OrderLines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReplacementReason",
                schema: "tentacion_sana",
                table: "OrderLines",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SaleQuantity",
                schema: "tentacion_sana",
                table: "OrderLines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AssignedReplacementQuantity",
                schema: "tentacion_sana",
                table: "DeliveryLines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AssignedSaleQuantity",
                schema: "tentacion_sana",
                table: "DeliveryLines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DeliveredReplacementQuantity",
                schema: "tentacion_sana",
                table: "DeliveryLines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DeliveredSaleQuantity",
                schema: "tentacion_sana",
                table: "DeliveryLines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Existing records represent ordinary sold units. Preserve their financial
            // and physical meaning while introducing the explicit replacement split.
            migrationBuilder.Sql("""
                UPDATE tentacion_sana."OrderLines"
                SET "SaleQuantity" = "Quantity", "ReplacementQuantity" = 0;
                UPDATE tentacion_sana."DeliveryLines"
                SET "AssignedSaleQuantity" = "AssignedQuantity",
                    "DeliveredSaleQuantity" = "DeliveredQuantity",
                    "AssignedReplacementQuantity" = 0,
                    "DeliveredReplacementQuantity" = 0;
                UPDATE tentacion_sana."Payments"
                SET "PaymentDateUtc" = "ReceivedAtUtc";
                """);

            migrationBuilder.CreateTable(
                name: "PaymentEvidence",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Bytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentEvidence_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "tentacion_sana",
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEvidence_PaymentId",
                schema: "tentacion_sana",
                table: "PaymentEvidence",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEvidence_PublicId",
                schema: "tentacion_sana",
                table: "PaymentEvidence",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentEvidence",
                schema: "tentacion_sana");

            migrationBuilder.DropColumn(
                name: "Notes",
                schema: "tentacion_sana",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PaymentDateUtc",
                schema: "tentacion_sana",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "DeliveryLocation",
                schema: "tentacion_sana",
                table: "OrderSnapshots");

            migrationBuilder.DropColumn(
                name: "DeliveryReference",
                schema: "tentacion_sana",
                table: "OrderSnapshots");

            migrationBuilder.DropColumn(
                name: "LinesJson",
                schema: "tentacion_sana",
                table: "OrderSnapshots");

            migrationBuilder.DropColumn(
                name: "ReplacementNotes",
                schema: "tentacion_sana",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "ReplacementQuantity",
                schema: "tentacion_sana",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "ReplacementReason",
                schema: "tentacion_sana",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "SaleQuantity",
                schema: "tentacion_sana",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "AssignedReplacementQuantity",
                schema: "tentacion_sana",
                table: "DeliveryLines");

            migrationBuilder.DropColumn(
                name: "AssignedSaleQuantity",
                schema: "tentacion_sana",
                table: "DeliveryLines");

            migrationBuilder.DropColumn(
                name: "DeliveredReplacementQuantity",
                schema: "tentacion_sana",
                table: "DeliveryLines");

            migrationBuilder.DropColumn(
                name: "DeliveredSaleQuantity",
                schema: "tentacion_sana",
                table: "DeliveryLines");
        }
    }
}
