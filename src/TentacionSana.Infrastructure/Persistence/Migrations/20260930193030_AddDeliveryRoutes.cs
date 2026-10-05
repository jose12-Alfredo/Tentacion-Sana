using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TentacionSana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryRoutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeliveryRoutes",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromisedDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryRoutes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryRouteStops",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryRouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryRouteStops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryRouteStops_Deliveries_DeliveryId",
                        column: x => x.DeliveryId,
                        principalSchema: "tentacion_sana",
                        principalTable: "Deliveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeliveryRouteStops_DeliveryRoutes_DeliveryRouteId",
                        column: x => x.DeliveryRouteId,
                        principalSchema: "tentacion_sana",
                        principalTable: "DeliveryRoutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryRoutes_DriverUserId_PromisedDateUtc",
                schema: "tentacion_sana",
                table: "DeliveryRoutes",
                columns: new[] { "DriverUserId", "PromisedDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryRouteStops_DeliveryId",
                schema: "tentacion_sana",
                table: "DeliveryRouteStops",
                column: "DeliveryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryRouteStops_DeliveryRouteId_Position",
                schema: "tentacion_sana",
                table: "DeliveryRouteStops",
                columns: new[] { "DeliveryRouteId", "Position" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeliveryRouteStops",
                schema: "tentacion_sana");

            migrationBuilder.DropTable(
                name: "DeliveryRoutes",
                schema: "tentacion_sana");
        }
    }
}
