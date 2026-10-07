using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TentacionSana.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261007190000_ReconcileVoidedProductionSupplies")]
public sealed class ReconcileVoidedProductionSupplies : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Earlier deletions removed finished goods but left their ingredient consumption in stock.
        // A batch already reversed by the current application has a net quantity of zero.
        migrationBuilder.Sql("""
            CREATE TEMP TABLE voided_supply_reversals ON COMMIT DROP AS
            SELECT b."Id" AS "BatchId", b."Number" AS "BatchNumber", m."SupplyId",
                   COALESCE(b."VoidedByUserId", b."CreatedByUserId") AS "UserId",
                   -SUM(m."Quantity") AS "Quantity"
            FROM tentacion_sana."ProductionBatches" AS b
            JOIN tentacion_sana."SupplyMovements" AS m ON m."ProductionBatchId" = b."Id"
            WHERE b."IsVoided"
              AND m."Kind" IN ('ProductionConsumption', 'ProductionCorrection')
            GROUP BY b."Id", b."Number", b."VoidedByUserId", b."CreatedByUserId", m."SupplyId"
            HAVING SUM(m."Quantity") <> 0;

            UPDATE tentacion_sana."SupplyBalances" AS balance
            SET "Quantity" = balance."Quantity" + reversal."Quantity",
                "Version" = balance."Version" + 1
            FROM (SELECT "SupplyId", SUM("Quantity") AS "Quantity"
                  FROM voided_supply_reversals GROUP BY "SupplyId") AS reversal
            WHERE balance."SupplyId" = reversal."SupplyId";

            INSERT INTO tentacion_sana."SupplyMovements"
                ("Id", "SupplyId", "ProductionBatchId", "PurchaseId", "Kind", "Quantity",
                 "HistoricalBaseUnitCost", "Reason", "UserId", "OccurredAtUtc")
            SELECT gen_random_uuid(), reversal."SupplyId", reversal."BatchId", NULL,
                   'ProductionCorrection', reversal."Quantity", NULL,
                   'Reversión histórica del consumo de lote eliminado ' || reversal."BatchNumber",
                   reversal."UserId", now()
            FROM voided_supply_reversals AS reversal;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Historical stock reconciliation is intentionally not reversed.
    }
}
