using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TentacionSana.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261012210000_RepairCompletedOrderStatuses")]
public sealed class RepairCompletedOrderStatuses : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            WITH eligible AS (
                SELECT o."Id", COALESCE((
                    SELECT h."UserId"
                    FROM tentacion_sana."Deliveries" d
                    JOIN tentacion_sana."DeliveryStatusHistory" h ON h."DeliveryId" = d."Id"
                    WHERE d."OrderId" = o."Id" AND h."NewStatus" = 'Delivered'
                    ORDER BY h."ChangedAtUtc" DESC LIMIT 1
                ), o."UpdatedByUserId") AS "ActorId"
                FROM tentacion_sana."Orders" o
                WHERE o."Status" = 'Ready' AND o."ArchivedAtUtc" IS NULL
                  AND EXISTS (SELECT 1 FROM tentacion_sana."Deliveries" d WHERE d."OrderId" = o."Id" AND d."Status" = 'Delivered')
                  AND NOT EXISTS (SELECT 1 FROM tentacion_sana."Deliveries" d WHERE d."OrderId" = o."Id" AND d."Status" NOT IN ('Delivered', 'Cancelled'))
                  AND NOT EXISTS (SELECT 1 FROM tentacion_sana."StockReservations" r WHERE r."OrderId" = o."Id" AND r."IsActive")
                  AND NOT EXISTS (
                      SELECT 1 FROM tentacion_sana."OrderLines" l
                      WHERE l."OrderId" = o."Id" AND l."IsActive"
                        AND l."Quantity" > COALESCE((
                            SELECT SUM(dl."DeliveredQuantity")
                            FROM tentacion_sana."DeliveryLines" dl
                            JOIN tentacion_sana."Deliveries" d ON d."Id" = dl."DeliveryId"
                            WHERE d."OrderId" = o."Id" AND d."Status" = 'Delivered' AND dl."OrderLineId" = l."Id"
                        ), 0)
                  )
            ), repaired AS (
                UPDATE tentacion_sana."Orders" o
                SET "Status" = 'Delivered', "UpdatedAtUtc" = CURRENT_TIMESTAMP,
                    "UpdatedByUserId" = eligible."ActorId", "Version" = o."Version" + 1
                FROM eligible WHERE o."Id" = eligible."Id"
                RETURNING o."Id", o."UpdatedByUserId"
            )
            INSERT INTO tentacion_sana."OrderStatusHistory"
                ("Id", "OrderId", "PreviousStatus", "NewStatus", "ChangedByUserId", "Reason", "ChangedAtUtc")
            SELECT gen_random_uuid(), "Id", 'Ready', 'Delivered', "UpdatedByUserId",
                   'Estado corregido tras una entrega completa ya registrada.', CURRENT_TIMESTAMP
            FROM repaired;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // A completed delivery cannot safely be reverted by rolling back code.
    }
}
