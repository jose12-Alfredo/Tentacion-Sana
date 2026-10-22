using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TentacionSana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWeightedAverageSupplyCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AverageUnitCost",
                schema: "tentacion_sana",
                table: "SupplyBalances",
                type: "numeric(16,6)",
                precision: 16,
                scale: 6,
                nullable: true);

            migrationBuilder.Sql("""
                WITH RECURSIVE ordered AS (
                    SELECT movement.*,
                           ROW_NUMBER() OVER (PARTITION BY "SupplyId" ORDER BY "OccurredAtUtc", "Id") AS rn
                    FROM tentacion_sana."SupplyMovements" AS movement
                ), valuation AS (
                    SELECT "SupplyId", rn, "Quantity"::numeric AS running_quantity,
                           CASE WHEN "Quantity" > 0 AND "HistoricalBaseUnitCost" IS NOT NULL
                                     AND "Kind" IN ('Purchase', 'PositiveAdjustment')
                                THEN "HistoricalBaseUnitCost"::numeric ELSE NULL::numeric END AS average_cost
                    FROM ordered WHERE rn = 1
                    UNION ALL
                    SELECT item."SupplyId", item.rn,
                           value.running_quantity + item."Quantity",
                           CASE WHEN item."Quantity" > 0 AND item."HistoricalBaseUnitCost" IS NOT NULL
                                     AND item."Kind" IN ('Purchase', 'PositiveAdjustment')
                                THEN ((value.running_quantity * COALESCE(value.average_cost, 0)) +
                                      (item."Quantity" * item."HistoricalBaseUnitCost")) /
                                      NULLIF(value.running_quantity + item."Quantity", 0)
                                ELSE value.average_cost END
                    FROM valuation AS value
                    JOIN ordered AS item ON item."SupplyId" = value."SupplyId" AND item.rn = value.rn + 1
                ), latest AS (
                    SELECT DISTINCT ON ("SupplyId") "SupplyId", ROUND(average_cost, 6) AS average_cost
                    FROM valuation ORDER BY "SupplyId", rn DESC
                )
                UPDATE tentacion_sana."SupplyBalances" AS balance
                SET "AverageUnitCost" = latest.average_cost
                FROM latest WHERE balance."SupplyId" = latest."SupplyId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AverageUnitCost",
                schema: "tentacion_sana",
                table: "SupplyBalances");
        }
    }
}
