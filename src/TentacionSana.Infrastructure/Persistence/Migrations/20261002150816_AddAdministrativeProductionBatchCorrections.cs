using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TentacionSana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdministrativeProductionBatchCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVoided",
                schema: "tentacion_sana",
                table: "ProductionBatches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                schema: "tentacion_sana",
                table: "ProductionBatches",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VoidedAtUtc",
                schema: "tentacion_sana",
                table: "ProductionBatches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VoidedByUserId",
                schema: "tentacion_sana",
                table: "ProductionBatches",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsVoided",
                schema: "tentacion_sana",
                table: "ProductionBatches");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                schema: "tentacion_sana",
                table: "ProductionBatches");

            migrationBuilder.DropColumn(
                name: "VoidedAtUtc",
                schema: "tentacion_sana",
                table: "ProductionBatches");

            migrationBuilder.DropColumn(
                name: "VoidedByUserId",
                schema: "tentacion_sana",
                table: "ProductionBatches");
        }
    }
}
