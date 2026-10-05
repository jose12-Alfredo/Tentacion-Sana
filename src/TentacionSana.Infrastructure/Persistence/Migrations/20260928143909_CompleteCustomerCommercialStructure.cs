using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TentacionSana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteCustomerCommercialStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ContactId",
                schema: "tentacion_sana",
                table: "DeliveryPoints",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                schema: "tentacion_sana",
                table: "DeliveryPoints",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "tentacion_sana",
                table: "DeliveryPoints",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "tentacion_sana",
                table: "DeliveryPoints",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Other");

            migrationBuilder.AddColumn<string>(
                name: "Location",
                schema: "tentacion_sana",
                table: "DeliveryPoints",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentResponsiblePartyId",
                schema: "tentacion_sana",
                table: "DeliveryPoints",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "tentacion_sana",
                table: "Customers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "tentacion_sana",
                table: "Customers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Individual");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                schema: "tentacion_sana",
                table: "Customers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                schema: "tentacion_sana",
                table: "CustomerContacts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                schema: "tentacion_sana",
                table: "CustomerContacts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "tentacion_sana",
                table: "CustomerContacts",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                schema: "tentacion_sana",
                table: "CustomerContacts",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentResponsibleParties",
                schema: "tentacion_sana",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentResponsibleParties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentResponsibleParties_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "tentacion_sana",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryPoints_ContactId",
                schema: "tentacion_sana",
                table: "DeliveryPoints",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryPoints_PaymentResponsiblePartyId",
                schema: "tentacion_sana",
                table: "DeliveryPoints",
                column: "PaymentResponsiblePartyId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentResponsibleParties_CustomerId_IsActive",
                schema: "tentacion_sana",
                table: "PaymentResponsibleParties",
                columns: new[] { "CustomerId", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryPoints_CustomerContacts_ContactId",
                schema: "tentacion_sana",
                table: "DeliveryPoints",
                column: "ContactId",
                principalSchema: "tentacion_sana",
                principalTable: "CustomerContacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryPoints_PaymentResponsibleParties_PaymentResponsible~",
                schema: "tentacion_sana",
                table: "DeliveryPoints",
                column: "PaymentResponsiblePartyId",
                principalSchema: "tentacion_sana",
                principalTable: "PaymentResponsibleParties",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryPoints_CustomerContacts_ContactId",
                schema: "tentacion_sana",
                table: "DeliveryPoints");

            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryPoints_PaymentResponsibleParties_PaymentResponsible~",
                schema: "tentacion_sana",
                table: "DeliveryPoints");

            migrationBuilder.DropTable(
                name: "PaymentResponsibleParties",
                schema: "tentacion_sana");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryPoints_ContactId",
                schema: "tentacion_sana",
                table: "DeliveryPoints");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryPoints_PaymentResponsiblePartyId",
                schema: "tentacion_sana",
                table: "DeliveryPoints");

            migrationBuilder.DropColumn(
                name: "ContactId",
                schema: "tentacion_sana",
                table: "DeliveryPoints");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                schema: "tentacion_sana",
                table: "DeliveryPoints");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "tentacion_sana",
                table: "DeliveryPoints");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "tentacion_sana",
                table: "DeliveryPoints");

            migrationBuilder.DropColumn(
                name: "Location",
                schema: "tentacion_sana",
                table: "DeliveryPoints");

            migrationBuilder.DropColumn(
                name: "PaymentResponsiblePartyId",
                schema: "tentacion_sana",
                table: "DeliveryPoints");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "tentacion_sana",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "tentacion_sana",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                schema: "tentacion_sana",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                schema: "tentacion_sana",
                table: "CustomerContacts");

            migrationBuilder.DropColumn(
                name: "Email",
                schema: "tentacion_sana",
                table: "CustomerContacts");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "tentacion_sana",
                table: "CustomerContacts");

            migrationBuilder.DropColumn(
                name: "Role",
                schema: "tentacion_sana",
                table: "CustomerContacts");
        }
    }
}
