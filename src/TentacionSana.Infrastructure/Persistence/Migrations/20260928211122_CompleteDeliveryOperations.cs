using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TentacionSana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteDeliveryOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeliveryEvidence",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Bytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryEvidence_Deliveries_DeliveryId",
                        column: x => x.DeliveryId,
                        principalSchema: "tentacion_sana",
                        principalTable: "Deliveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryEvidence_DeliveryId_UploadedAtUtc",
                schema: "tentacion_sana",
                table: "DeliveryEvidence",
                columns: new[] { "DeliveryId", "UploadedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryEvidence_PublicId",
                schema: "tentacion_sana",
                table: "DeliveryEvidence",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeliveryEvidence",
                schema: "tentacion_sana");
        }
    }
}
