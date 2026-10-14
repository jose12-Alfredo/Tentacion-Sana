using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TentacionSana.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261014000000_AddRenditionRepayments")]
public sealed class AddRenditionRepayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(name: "PaidAmount", schema: "tentacion_sana", table: "CashPayables", type: "numeric(14,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<int>(name: "Version", schema: "tentacion_sana", table: "CashPayables", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<Guid>(name: "CashPayableId", schema: "tentacion_sana", table: "CashMovements", type: "uuid", nullable: true);
        migrationBuilder.CreateIndex(name: "IX_CashPayables_CreatedByUserId", schema: "tentacion_sana", table: "CashPayables", column: "CreatedByUserId");
        migrationBuilder.CreateIndex(name: "IX_CashMovements_CashPayableId", schema: "tentacion_sana", table: "CashMovements", column: "CashPayableId");
        migrationBuilder.AddForeignKey(name: "FK_CashMovements_CashPayables_CashPayableId", schema: "tentacion_sana", table: "CashMovements", column: "CashPayableId", principalSchema: "tentacion_sana", principalTable: "CashPayables", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_CashMovements_CashPayables_CashPayableId", schema: "tentacion_sana", table: "CashMovements");
        migrationBuilder.DropIndex(name: "IX_CashMovements_CashPayableId", schema: "tentacion_sana", table: "CashMovements");
        migrationBuilder.DropIndex(name: "IX_CashPayables_CreatedByUserId", schema: "tentacion_sana", table: "CashPayables");
        migrationBuilder.DropColumn(name: "CashPayableId", schema: "tentacion_sana", table: "CashMovements");
        migrationBuilder.DropColumn(name: "Version", schema: "tentacion_sana", table: "CashPayables");
        migrationBuilder.DropColumn(name: "PaidAmount", schema: "tentacion_sana", table: "CashPayables");
    }
}
