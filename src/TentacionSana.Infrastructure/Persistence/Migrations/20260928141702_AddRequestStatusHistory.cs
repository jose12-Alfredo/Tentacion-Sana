using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TentacionSana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestStatusHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RequestStatusHistory",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    NewStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestStatusHistory_ProductRequests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "tentacion_sana",
                        principalTable: "ProductRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RequestStatusHistory_RequestId_ChangedAtUtc",
                schema: "tentacion_sana",
                table: "RequestStatusHistory",
                columns: new[] { "RequestId", "ChangedAtUtc" });

            migrationBuilder.Sql(
                """
                INSERT INTO tentacion_sana."RequestStatusHistory"
                    ("Id", "RequestId", "PreviousStatus", "NewStatus", "ChangedByUserId", "Reason", "ChangedAtUtc")
                SELECT md5("Id"::text || ':initial-status')::uuid,
                       "Id", NULL, "Status", NULL,
                       'Estado existente al incorporar el historial.', "CreatedAtUtc"
                FROM tentacion_sana."ProductRequests";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequestStatusHistory",
                schema: "tentacion_sana");
        }
    }
}
