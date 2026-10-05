using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TentacionSana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTastingQuantities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TastingQuantity",
                schema: "tentacion_sana",
                table: "OrderLines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AssignedTastingQuantity",
                schema: "tentacion_sana",
                table: "DeliveryLines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DeliveredTastingQuantity",
                schema: "tentacion_sana",
                table: "DeliveryLines",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TastingQuantity",
                schema: "tentacion_sana",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "AssignedTastingQuantity",
                schema: "tentacion_sana",
                table: "DeliveryLines");

            migrationBuilder.DropColumn(
                name: "DeliveredTastingQuantity",
                schema: "tentacion_sana",
                table: "DeliveryLines");
        }
    }
}
