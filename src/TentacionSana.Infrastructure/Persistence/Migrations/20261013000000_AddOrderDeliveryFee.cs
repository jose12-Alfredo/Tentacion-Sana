using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TentacionSana.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261013000000_AddOrderDeliveryFee")]
public sealed class AddOrderDeliveryFee : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "DeliveryFee",
            schema: "tentacion_sana",
            table: "Orders",
            type: "numeric(12,2)",
            nullable: false,
            defaultValue: 0m);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "DeliveryFee", schema: "tentacion_sana", table: "Orders");
    }
}
