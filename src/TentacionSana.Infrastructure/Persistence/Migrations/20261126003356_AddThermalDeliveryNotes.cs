using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TentacionSana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddThermalDeliveryNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "DeliveryNoteNumberSequence",
                schema: "tentacion_sana");

            migrationBuilder.CreateTable(
                name: "DeliveryNotes",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Scope = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    TemplateVersion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Pdf = table.Column<byte[]>(type: "bytea", nullable: false),
                    Png = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryNotes_AspNetUsers_IssuedByUserId",
                        column: x => x.IssuedByUserId,
                        principalSchema: "tentacion_sana",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeliveryNotes_Deliveries_DeliveryId",
                        column: x => x.DeliveryId,
                        principalSchema: "tentacion_sana",
                        principalTable: "Deliveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeliveryNotes_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "tentacion_sana",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_DeliveryId",
                schema: "tentacion_sana",
                table: "DeliveryNotes",
                column: "DeliveryId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_IssuedByUserId",
                schema: "tentacion_sana",
                table: "DeliveryNotes",
                column: "IssuedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_Number",
                schema: "tentacion_sana",
                table: "DeliveryNotes",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_OrderId_Fingerprint",
                schema: "tentacion_sana",
                table: "DeliveryNotes",
                columns: new[] { "OrderId", "Fingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_RequestId",
                schema: "tentacion_sana",
                table: "DeliveryNotes",
                column: "RequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeliveryNotes",
                schema: "tentacion_sana");

            migrationBuilder.DropSequence(
                name: "DeliveryNoteNumberSequence",
                schema: "tentacion_sana");
        }
    }
}
