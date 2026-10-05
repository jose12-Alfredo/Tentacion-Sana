using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TentacionSana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderTastingReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "TastingReason", schema: "tentacion_sana", table: "OrderLines", type: "character varying(80)", maxLength: 80, nullable: true);
            migrationBuilder.AddColumn<string>(name: "TastingNotes", schema: "tentacion_sana", table: "OrderLines", type: "character varying(500)", maxLength: 500, nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "TastingReason", schema: "tentacion_sana", table: "OrderLines");
            migrationBuilder.DropColumn(name: "TastingNotes", schema: "tentacion_sana", table: "OrderLines");
        }
    }
}
