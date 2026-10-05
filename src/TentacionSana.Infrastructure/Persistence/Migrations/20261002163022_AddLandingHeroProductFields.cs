using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TentacionSana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLandingHeroProductFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLandingFeatured",
                schema: "tentacion_sana",
                table: "ProductPublications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LandingBadgeText",
                schema: "tentacion_sana",
                table: "ProductPublications",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LandingOrder",
                schema: "tentacion_sana",
                table: "ProductPublications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LandingSubtitle",
                schema: "tentacion_sana",
                table: "ProductPublications",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LandingTitle",
                schema: "tentacion_sana",
                table: "ProductPublications",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductPublications_IsLandingFeatured_LandingOrder",
                schema: "tentacion_sana",
                table: "ProductPublications",
                columns: new[] { "IsLandingFeatured", "LandingOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductPublications_IsLandingFeatured_LandingOrder",
                schema: "tentacion_sana",
                table: "ProductPublications");

            migrationBuilder.DropColumn(
                name: "IsLandingFeatured",
                schema: "tentacion_sana",
                table: "ProductPublications");

            migrationBuilder.DropColumn(
                name: "LandingBadgeText",
                schema: "tentacion_sana",
                table: "ProductPublications");

            migrationBuilder.DropColumn(
                name: "LandingOrder",
                schema: "tentacion_sana",
                table: "ProductPublications");

            migrationBuilder.DropColumn(
                name: "LandingSubtitle",
                schema: "tentacion_sana",
                table: "ProductPublications");

            migrationBuilder.DropColumn(
                name: "LandingTitle",
                schema: "tentacion_sana",
                table: "ProductPublications");
        }
    }
}
