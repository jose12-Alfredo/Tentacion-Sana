using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TentacionSana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountingAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccountingAccountId",
                schema: "tentacion_sana",
                table: "CashMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccountingAccounts",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingAccounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_AccountingAccountId",
                schema: "tentacion_sana",
                table: "CashMovements",
                column: "AccountingAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingAccounts_Code",
                schema: "tentacion_sana",
                table: "AccountingAccounts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingAccounts_Kind_IsActive",
                schema: "tentacion_sana",
                table: "AccountingAccounts",
                columns: new[] { "Kind", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingAccounts_Name",
                schema: "tentacion_sana",
                table: "AccountingAccounts",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CashMovements_AccountingAccounts_AccountingAccountId",
                schema: "tentacion_sana",
                table: "CashMovements",
                column: "AccountingAccountId",
                principalSchema: "tentacion_sana",
                principalTable: "AccountingAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                INSERT INTO tentacion_sana."AccountingAccounts"
                    ("Id", "Code", "Name", "Kind", "IsActive", "CreatedAtUtc", "CreatedByUserId")
                VALUES
                    ('10000000-0000-0000-0000-000000000001', '4101', 'Cobro manual', 'Income', TRUE, NOW(), '00000000-0000-0000-0000-000000000000'),
                    ('10000000-0000-0000-0000-000000000002', '4102', 'Devolución', 'Income', TRUE, NOW(), '00000000-0000-0000-0000-000000000000'),
                    ('10000000-0000-0000-0000-000000000003', '4199', 'Otro ingreso', 'Income', TRUE, NOW(), '00000000-0000-0000-0000-000000000000'),
                    ('10000000-0000-0000-0000-000000000004', '2101', 'Anticipo', 'Liability', TRUE, NOW(), '00000000-0000-0000-0000-000000000000'),
                    ('10000000-0000-0000-0000-000000000005', '3101', 'Aporte de socio', 'Equity', TRUE, NOW(), '00000000-0000-0000-0000-000000000000'),
                    ('10000000-0000-0000-0000-000000000006', '5101', 'Ingredientes', 'DirectCost', TRUE, NOW(), '00000000-0000-0000-0000-000000000000'),
                    ('10000000-0000-0000-0000-000000000007', '5102', 'Envases', 'DirectCost', TRUE, NOW(), '00000000-0000-0000-0000-000000000000'),
                    ('10000000-0000-0000-0000-000000000008', '5103', 'Etiquetas', 'DirectCost', TRUE, NOW(), '00000000-0000-0000-0000-000000000000'),
                    ('10000000-0000-0000-0000-000000000009', '5201', 'Delivery', 'OperatingExpense', TRUE, NOW(), '00000000-0000-0000-0000-000000000000'),
                    ('10000000-0000-0000-0000-000000000010', '5202', 'Transporte', 'OperatingExpense', TRUE, NOW(), '00000000-0000-0000-0000-000000000000'),
                    ('10000000-0000-0000-0000-000000000011', '5203', 'Pasajes', 'OperatingExpense', TRUE, NOW(), '00000000-0000-0000-0000-000000000000'),
                    ('10000000-0000-0000-0000-000000000012', '5204', 'Publicidad', 'OperatingExpense', TRUE, NOW(), '00000000-0000-0000-0000-000000000000'),
                    ('10000000-0000-0000-0000-000000000013', '5205', 'Servicios', 'OperatingExpense', TRUE, NOW(), '00000000-0000-0000-0000-000000000000'),
                    ('10000000-0000-0000-0000-000000000014', '5206', 'Utensilios menores', 'OperatingExpense', TRUE, NOW(), '00000000-0000-0000-0000-000000000000'),
                    ('10000000-0000-0000-0000-000000000015', '5299', 'Otros', 'OperatingExpense', TRUE, NOW(), '00000000-0000-0000-0000-000000000000'),
                    ('10000000-0000-0000-0000-000000000016', '1501', 'Equipos', 'Asset', TRUE, NOW(), '00000000-0000-0000-0000-000000000000'),
                    ('10000000-0000-0000-0000-000000000017', '1502', 'Equipos de cocina', 'Asset', TRUE, NOW(), '00000000-0000-0000-0000-000000000000');

                UPDATE tentacion_sana."CashMovements" AS movement
                SET "AccountingAccountId" = account."Id"
                FROM tentacion_sana."AccountingAccounts" AS account
                WHERE movement."Source" = 'Manual' AND movement."Category" = account."Name";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashMovements_AccountingAccounts_AccountingAccountId",
                schema: "tentacion_sana",
                table: "CashMovements");

            migrationBuilder.DropTable(
                name: "AccountingAccounts",
                schema: "tentacion_sana");

            migrationBuilder.DropIndex(
                name: "IX_CashMovements_AccountingAccountId",
                schema: "tentacion_sana",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "AccountingAccountId",
                schema: "tentacion_sana",
                table: "CashMovements");
        }
    }
}
