using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TentacionSana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplyInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "YieldQuantity",
                schema: "tentacion_sana",
                table: "RecipeVersions",
                type: "numeric(14,4)",
                precision: 14,
                scale: 4,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.CreateTable(
                name: "Supplies",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    BaseUnit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PurchasePresentation = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    BaseQuantityPerPackage = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Supplies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecipeIngredients",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequiredQuantity = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeIngredients_RecipeVersions_RecipeVersionId",
                        column: x => x.RecipeVersionId,
                        principalSchema: "tentacion_sana",
                        principalTable: "RecipeVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecipeIngredients_Supplies_SupplyId",
                        column: x => x.SupplyId,
                        principalSchema: "tentacion_sana",
                        principalTable: "Supplies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplyBalances",
                schema: "tentacion_sana",
                columns: table => new
                {
                    SupplyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(16,4)", precision: 16, scale: 4, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplyBalances", x => x.SupplyId);
                    table.ForeignKey(
                        name: "FK_SupplyBalances_Supplies_SupplyId",
                        column: x => x.SupplyId,
                        principalSchema: "tentacion_sana",
                        principalTable: "Supplies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplyPurchases",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchasedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PackageQuantity = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    BaseQuantityPerPackage = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    TotalBaseQuantity = table.Column<decimal>(type: "numeric(16,4)", precision: 16, scale: 4, nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    RegisteredByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplyPurchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplyPurchases_Supplies_SupplyId",
                        column: x => x.SupplyId,
                        principalSchema: "tentacion_sana",
                        principalTable: "Supplies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplyStockCounts",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedQuantity = table.Column<decimal>(type: "numeric(16,4)", precision: 16, scale: 4, nullable: false),
                    CountedQuantity = table.Column<decimal>(type: "numeric(16,4)", precision: 16, scale: 4, nullable: false),
                    Difference = table.Column<decimal>(type: "numeric(16,4)", precision: 16, scale: 4, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CountedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplyStockCounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplyStockCounts_Supplies_SupplyId",
                        column: x => x.SupplyId,
                        principalSchema: "tentacion_sana",
                        principalTable: "Supplies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplyMovements",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductionBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(16,4)", precision: 16, scale: 4, nullable: false),
                    HistoricalBaseUnitCost = table.Column<decimal>(type: "numeric(16,6)", precision: 16, scale: 6, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplyMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplyMovements_ProductionBatches_ProductionBatchId",
                        column: x => x.ProductionBatchId,
                        principalSchema: "tentacion_sana",
                        principalTable: "ProductionBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplyMovements_Supplies_SupplyId",
                        column: x => x.SupplyId,
                        principalSchema: "tentacion_sana",
                        principalTable: "Supplies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplyMovements_SupplyPurchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalSchema: "tentacion_sana",
                        principalTable: "SupplyPurchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredients_RecipeVersionId_SupplyId",
                schema: "tentacion_sana",
                table: "RecipeIngredients",
                columns: new[] { "RecipeVersionId", "SupplyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredients_SupplyId",
                schema: "tentacion_sana",
                table: "RecipeIngredients",
                column: "SupplyId");

            migrationBuilder.CreateIndex(
                name: "IX_Supplies_Name",
                schema: "tentacion_sana",
                table: "Supplies",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplyMovements_ProductionBatchId",
                schema: "tentacion_sana",
                table: "SupplyMovements",
                column: "ProductionBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplyMovements_PurchaseId",
                schema: "tentacion_sana",
                table: "SupplyMovements",
                column: "PurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplyMovements_SupplyId_OccurredAtUtc",
                schema: "tentacion_sana",
                table: "SupplyMovements",
                columns: new[] { "SupplyId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplyPurchases_SupplyId_PurchasedAtUtc",
                schema: "tentacion_sana",
                table: "SupplyPurchases",
                columns: new[] { "SupplyId", "PurchasedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplyStockCounts_SupplyId_CountedAtUtc",
                schema: "tentacion_sana",
                table: "SupplyStockCounts",
                columns: new[] { "SupplyId", "CountedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeIngredients",
                schema: "tentacion_sana");

            migrationBuilder.DropTable(
                name: "SupplyBalances",
                schema: "tentacion_sana");

            migrationBuilder.DropTable(
                name: "SupplyMovements",
                schema: "tentacion_sana");

            migrationBuilder.DropTable(
                name: "SupplyStockCounts",
                schema: "tentacion_sana");

            migrationBuilder.DropTable(
                name: "SupplyPurchases",
                schema: "tentacion_sana");

            migrationBuilder.DropTable(
                name: "Supplies",
                schema: "tentacion_sana");

            migrationBuilder.DropColumn(
                name: "YieldQuantity",
                schema: "tentacion_sana",
                table: "RecipeVersions");
        }
    }
}
