using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TentacionSana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductRequests",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                    Origin = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ConsentAccepted = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductRequestLines",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductRequestLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductRequestLines_ProductRequests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "tentacion_sana",
                        principalTable: "ProductRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductRequestLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "tentacion_sana",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductRequestLines_ProductId",
                schema: "tentacion_sana",
                table: "ProductRequestLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRequestLines_RequestId",
                schema: "tentacion_sana",
                table: "ProductRequestLines",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRequests_Status_CreatedAtUtc",
                schema: "tentacion_sana",
                table: "ProductRequests",
                columns: new[] { "Status", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductRequestLines",
                schema: "tentacion_sana");

            migrationBuilder.DropTable(
                name: "ProductRequests",
                schema: "tentacion_sana");
        }
    }
}
