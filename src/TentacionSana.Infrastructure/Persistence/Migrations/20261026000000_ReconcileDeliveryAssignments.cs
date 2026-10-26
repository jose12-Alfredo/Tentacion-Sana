using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TentacionSana.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261026000000_ReconcileDeliveryAssignments")]
public sealed class ReconcileDeliveryAssignments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var kind in new[] { "Sale", "Replacement", "Tasting" })
        {
            migrationBuilder.Sql($$"""
                WITH pending AS (
                    SELECT dl."Id", dl."OrderLineId",
                           dl."Assigned{{kind}}Quantity" - dl."Delivered{{kind}}Quantity" AS pending,
                           SUM(dl."Assigned{{kind}}Quantity" - dl."Delivered{{kind}}Quantity") OVER (
                               PARTITION BY dl."OrderLineId" ORDER BY d."ScheduledAtUtc" DESC, d."Id" DESC
                               ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING) AS earlier_pending
                    FROM tentacion_sana."DeliveryLines" dl
                    JOIN tentacion_sana."Deliveries" d ON d."Id" = dl."DeliveryId"
                    WHERE d."Status" NOT IN ('Delivered', 'Cancelled')
                ), excess AS (
                    SELECT p."Id", p.pending, COALESCE(p.earlier_pending, 0) AS earlier_pending,
                           GREATEST(0, (
                               SELECT COALESCE(SUM(all_dl."Assigned{{kind}}Quantity" - all_dl."Delivered{{kind}}Quantity"), 0)
                               FROM tentacion_sana."DeliveryLines" all_dl
                               JOIN tentacion_sana."Deliveries" active_d ON active_d."Id" = all_dl."DeliveryId"
                               WHERE all_dl."OrderLineId" = p."OrderLineId" AND active_d."Status" NOT IN ('Delivered', 'Cancelled')
                           ) - GREATEST(0, ol."{{kind}}Quantity" - (
                               SELECT COALESCE(SUM(delivered_dl."Delivered{{kind}}Quantity"), 0)
                               FROM tentacion_sana."DeliveryLines" delivered_dl
                               WHERE delivered_dl."OrderLineId" = p."OrderLineId"
                           ))) AS excess
                    FROM pending p
                    JOIN tentacion_sana."OrderLines" ol ON ol."Id" = p."OrderLineId"
                )
                UPDATE tentacion_sana."DeliveryLines" dl
                SET "Assigned{{kind}}Quantity" = dl."Assigned{{kind}}Quantity" - LEAST(excess.pending, GREATEST(0, excess.excess - excess.earlier_pending))
                FROM excess
                WHERE dl."Id" = excess."Id" AND excess.excess > excess.earlier_pending;
                """);
        }

        migrationBuilder.Sql("""
            UPDATE tentacion_sana."DeliveryLines"
            SET "AssignedQuantity" = "AssignedSaleQuantity" + "AssignedReplacementQuantity" + "AssignedTastingQuantity"
            WHERE "AssignedQuantity" <> "AssignedSaleQuantity" + "AssignedReplacementQuantity" + "AssignedTastingQuantity";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) { }
}
