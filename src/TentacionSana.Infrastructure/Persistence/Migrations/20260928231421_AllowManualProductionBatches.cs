using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TentacionSana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowManualProductionBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "tentacion_sana",
                table: "SettlementObligations",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                schema: "tentacion_sana",
                table: "SettlementObligations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "RecipeVersionId",
                schema: "tentacion_sana",
                table: "ProductionBatches",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "Settlements",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HolderUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeclaredAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    ReceivedAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DifferenceReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Reference = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settlements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SettlementAllocations",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SettlementId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObligationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SettlementAllocations_SettlementObligations_ObligationId",
                        column: x => x.ObligationId,
                        principalSchema: "tentacion_sana",
                        principalTable: "SettlementObligations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SettlementAllocations_Settlements_SettlementId",
                        column: x => x.SettlementId,
                        principalSchema: "tentacion_sana",
                        principalTable: "Settlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SettlementObligations_HolderUserId_Status",
                schema: "tentacion_sana",
                table: "SettlementObligations",
                columns: new[] { "HolderUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SettlementAllocations_ObligationId",
                schema: "tentacion_sana",
                table: "SettlementAllocations",
                column: "ObligationId");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementAllocations_SettlementId_ObligationId",
                schema: "tentacion_sana",
                table: "SettlementAllocations",
                columns: new[] { "SettlementId", "ObligationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Settlements_HolderUserId_ReceivedAtUtc",
                schema: "tentacion_sana",
                table: "Settlements",
                columns: new[] { "HolderUserId", "ReceivedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SettlementAllocations",
                schema: "tentacion_sana");

            migrationBuilder.DropTable(
                name: "Settlements",
                schema: "tentacion_sana");

            migrationBuilder.DropIndex(
                name: "IX_SettlementObligations_HolderUserId_Status",
                schema: "tentacion_sana",
                table: "SettlementObligations");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "tentacion_sana",
                table: "SettlementObligations");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "tentacion_sana",
                table: "SettlementObligations");

            migrationBuilder.AlterColumn<Guid>(
                name: "RecipeVersionId",
                schema: "tentacion_sana",
                table: "ProductionBatches",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
