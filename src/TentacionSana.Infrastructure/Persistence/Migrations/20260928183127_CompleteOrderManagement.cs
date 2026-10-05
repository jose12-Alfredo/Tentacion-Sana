using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TentacionSana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteOrderManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UnitPrice",
                schema: "tentacion_sana",
                table: "OrderLines",
                newName: "StandardUnitPrice");

            migrationBuilder.CreateSequence(
                name: "OrderNumberSequence",
                schema: "tentacion_sana",
                startValue: 1001L);

            migrationBuilder.AddColumn<Guid>(
                name: "ContactId",
                schema: "tentacion_sana",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                schema: "tentacion_sana",
                table: "Orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "DeliveryPointId",
                schema: "tentacion_sana",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                schema: "tentacion_sana",
                table: "Orders",
                type: "character varying(1500)",
                maxLength: 1500,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Number",
                schema: "tentacion_sana",
                table: "Orders",
                type: "bigint",
                nullable: false,
                defaultValueSql: "nextval('\"tentacion_sana\".\"OrderNumberSequence\"')");

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentResponsiblePartyId",
                schema: "tentacion_sana",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                schema: "tentacion_sana",
                table: "Orders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PromisedAtUtc",
                schema: "tentacion_sana",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                schema: "tentacion_sana",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                schema: "tentacion_sana",
                table: "Orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "Version",
                schema: "tentacion_sana",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DiscountReason",
                schema: "tentacion_sana",
                table: "OrderLines",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SoldUnitPrice",
                schema: "tentacion_sana",
                table: "OrderLines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitDiscount",
                schema: "tentacion_sana",
                table: "OrderLines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql("""
                UPDATE tentacion_sana."Orders"
                SET "PaymentStatus" = 'Pending', "Version" = 1, "UpdatedAtUtc" = "CreatedAtUtc";
                UPDATE tentacion_sana."OrderLines"
                SET "SoldUnitPrice" = "StandardUnitPrice", "UnitDiscount" = 0;
                """);

            migrationBuilder.CreateTable(
                name: "OrderChangeHistory",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Field = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PreviousValue = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    NewValue = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderChangeHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderChangeHistory_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "tentacion_sana",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderSnapshots",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DeliveryPointLabel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    DeliveryAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ContactName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    PayerName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderSnapshots_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "tentacion_sana",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderStatusHistory",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NewStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderStatusHistory_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "tentacion_sana",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PromisedDateHistory",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NewDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromisedDateHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromisedDateHistory_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "tentacion_sana",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockReservations",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ShortageQuantity = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReleasedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockReservations_OrderLines_OrderLineId",
                        column: x => x.OrderLineId,
                        principalSchema: "tentacion_sana",
                        principalTable: "OrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockReservations_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "tentacion_sana",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockReservations_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "tentacion_sana",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ContactId",
                schema: "tentacion_sana",
                table: "Orders",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_DeliveryPointId",
                schema: "tentacion_sana",
                table: "Orders",
                column: "DeliveryPointId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Number",
                schema: "tentacion_sana",
                table: "Orders",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PaymentResponsiblePartyId",
                schema: "tentacion_sana",
                table: "Orders",
                column: "PaymentResponsiblePartyId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderChangeHistory_OrderId_ChangedAtUtc",
                schema: "tentacion_sana",
                table: "OrderChangeHistory",
                columns: new[] { "OrderId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderSnapshots_OrderId",
                schema: "tentacion_sana",
                table: "OrderSnapshots",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistory_OrderId_ChangedAtUtc",
                schema: "tentacion_sana",
                table: "OrderStatusHistory",
                columns: new[] { "OrderId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PromisedDateHistory_OrderId_ChangedAtUtc",
                schema: "tentacion_sana",
                table: "PromisedDateHistory",
                columns: new[] { "OrderId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_OrderId",
                schema: "tentacion_sana",
                table: "StockReservations",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_OrderLineId",
                schema: "tentacion_sana",
                table: "StockReservations",
                column: "OrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_ProductId_IsActive",
                schema: "tentacion_sana",
                table: "StockReservations",
                columns: new[] { "ProductId", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_CustomerContacts_ContactId",
                schema: "tentacion_sana",
                table: "Orders",
                column: "ContactId",
                principalSchema: "tentacion_sana",
                principalTable: "CustomerContacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_DeliveryPoints_DeliveryPointId",
                schema: "tentacion_sana",
                table: "Orders",
                column: "DeliveryPointId",
                principalSchema: "tentacion_sana",
                principalTable: "DeliveryPoints",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_PaymentResponsibleParties_PaymentResponsiblePartyId",
                schema: "tentacion_sana",
                table: "Orders",
                column: "PaymentResponsiblePartyId",
                principalSchema: "tentacion_sana",
                principalTable: "PaymentResponsibleParties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_CustomerContacts_ContactId",
                schema: "tentacion_sana",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_DeliveryPoints_DeliveryPointId",
                schema: "tentacion_sana",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_PaymentResponsibleParties_PaymentResponsiblePartyId",
                schema: "tentacion_sana",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "OrderChangeHistory",
                schema: "tentacion_sana");

            migrationBuilder.DropTable(
                name: "OrderSnapshots",
                schema: "tentacion_sana");

            migrationBuilder.DropTable(
                name: "OrderStatusHistory",
                schema: "tentacion_sana");

            migrationBuilder.DropTable(
                name: "PromisedDateHistory",
                schema: "tentacion_sana");

            migrationBuilder.DropTable(
                name: "StockReservations",
                schema: "tentacion_sana");

            migrationBuilder.DropIndex(
                name: "IX_Orders_ContactId",
                schema: "tentacion_sana",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_DeliveryPointId",
                schema: "tentacion_sana",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_Number",
                schema: "tentacion_sana",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_PaymentResponsiblePartyId",
                schema: "tentacion_sana",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ContactId",
                schema: "tentacion_sana",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                schema: "tentacion_sana",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryPointId",
                schema: "tentacion_sana",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Notes",
                schema: "tentacion_sana",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Number",
                schema: "tentacion_sana",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentResponsiblePartyId",
                schema: "tentacion_sana",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                schema: "tentacion_sana",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PromisedAtUtc",
                schema: "tentacion_sana",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                schema: "tentacion_sana",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                schema: "tentacion_sana",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "tentacion_sana",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountReason",
                schema: "tentacion_sana",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "SoldUnitPrice",
                schema: "tentacion_sana",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "UnitDiscount",
                schema: "tentacion_sana",
                table: "OrderLines");

            migrationBuilder.DropSequence(
                name: "OrderNumberSequence",
                schema: "tentacion_sana");

            migrationBuilder.RenameColumn(
                name: "StandardUnitPrice",
                schema: "tentacion_sana",
                table: "OrderLines",
                newName: "UnitPrice");
        }
    }
}
