using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TentacionSana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinishedGoodsInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryMovements",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductionBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    HistoricalUnitCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    HistoricalTotalCost = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryMovements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductCostVersions",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    EstimatedUnitCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    EffectiveFromUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveToUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCostVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductionBatches",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProducedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GoodUnits = table.Column<int>(type: "integer", nullable: false),
                    WasteUnits = table.Column<int>(type: "integer", nullable: false),
                    RemainingUnits = table.Column<int>(type: "integer", nullable: false),
                    EstimatedUnitCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    EstimatedTotalCost = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductStockBalances",
                schema: "tentacion_sana",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhysicalQuantity = table.Column<int>(type: "integer", nullable: false),
                    ReservedQuantity = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductStockBalances", x => x.ProductId);
                });

            migrationBuilder.CreateTable(
                name: "RecipeVersions",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    EffectiveFromUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveToUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsValidated = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockCounts",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedQuantity = table.Column<int>(type: "integer", nullable: false),
                    CountedQuantity = table.Column<int>(type: "integer", nullable: false),
                    Difference = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CountedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockCounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryMovementAllocations",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MovementId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductionBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    HistoricalUnitCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryMovementAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryMovementAllocations_InventoryMovements_MovementId",
                        column: x => x.MovementId,
                        principalSchema: "tentacion_sana",
                        principalTable: "InventoryMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventoryMovementAllocations_ProductionBatches_ProductionBa~",
                        column: x => x.ProductionBatchId,
                        principalSchema: "tentacion_sana",
                        principalTable: "ProductionBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovementAllocations_MovementId",
                schema: "tentacion_sana",
                table: "InventoryMovementAllocations",
                column: "MovementId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovementAllocations_ProductionBatchId",
                schema: "tentacion_sana",
                table: "InventoryMovementAllocations",
                column: "ProductionBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_ProductId_OccurredAtUtc",
                schema: "tentacion_sana",
                table: "InventoryMovements",
                columns: new[] { "ProductId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductCostVersions_ProductId_EffectiveFromUtc",
                schema: "tentacion_sana",
                table: "ProductCostVersions",
                columns: new[] { "ProductId", "EffectiveFromUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionBatches_Number",
                schema: "tentacion_sana",
                table: "ProductionBatches",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeVersions_ProductId_Version",
                schema: "tentacion_sana",
                table: "RecipeVersions",
                columns: new[] { "ProductId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockCounts_ProductId_CountedAtUtc",
                schema: "tentacion_sana",
                table: "StockCounts",
                columns: new[] { "ProductId", "CountedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryMovementAllocations",
                schema: "tentacion_sana");

            migrationBuilder.DropTable(
                name: "ProductCostVersions",
                schema: "tentacion_sana");

            migrationBuilder.DropTable(
                name: "ProductStockBalances",
                schema: "tentacion_sana");

            migrationBuilder.DropTable(
                name: "RecipeVersions",
                schema: "tentacion_sana");

            migrationBuilder.DropTable(
                name: "StockCounts",
                schema: "tentacion_sana");

            migrationBuilder.DropTable(
                name: "InventoryMovements",
                schema: "tentacion_sana");

            migrationBuilder.DropTable(
                name: "ProductionBatches",
                schema: "tentacion_sana");
        }
    }
}
