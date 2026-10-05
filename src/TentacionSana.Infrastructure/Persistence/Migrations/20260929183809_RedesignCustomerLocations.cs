using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TentacionSana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RedesignCustomerLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ContactId",
                schema: "tentacion_sana",
                table: "PaymentResponsibleParties",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Label",
                schema: "tentacion_sana",
                table: "PaymentResponsibleParties",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageFileName",
                schema: "tentacion_sana",
                table: "DeliveryPoints",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageFormat",
                schema: "tentacion_sana",
                table: "DeliveryPoints",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImagePublicId",
                schema: "tentacion_sana",
                table: "DeliveryPoints",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                schema: "tentacion_sana",
                table: "DeliveryPoints",
                type: "character varying(1500)",
                maxLength: 1500,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                schema: "tentacion_sana",
                table: "Customers",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                schema: "tentacion_sana",
                table: "Customers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Individual");

            migrationBuilder.Sql("""
                UPDATE tentacion_sana."Customers"
                SET "Category" = CASE WHEN "Kind" = 'Individual' THEN 'Individual' ELSE 'Other' END;
                """);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                schema: "tentacion_sana",
                table: "Customers",
                type: "character varying(1500)",
                maxLength: 1500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeliveryPointId",
                schema: "tentacion_sana",
                table: "CustomerContacts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAccounting",
                schema: "tentacion_sana",
                table: "CustomerContacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsAdministrator",
                schema: "tentacion_sana",
                table: "CustomerContacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOrderContact",
                schema: "tentacion_sana",
                table: "CustomerContacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOwner",
                schema: "tentacion_sana",
                table: "CustomerContacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPaymentContact",
                schema: "tentacion_sana",
                table: "CustomerContacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsReceptionContact",
                schema: "tentacion_sana",
                table: "CustomerContacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentResponsibleParties_ContactId",
                schema: "tentacion_sana",
                table: "PaymentResponsibleParties",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerContacts_DeliveryPointId",
                schema: "tentacion_sana",
                table: "CustomerContacts",
                column: "DeliveryPointId");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerContacts_DeliveryPoints_DeliveryPointId",
                schema: "tentacion_sana",
                table: "CustomerContacts",
                column: "DeliveryPointId",
                principalSchema: "tentacion_sana",
                principalTable: "DeliveryPoints",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentResponsibleParties_CustomerContacts_ContactId",
                schema: "tentacion_sana",
                table: "PaymentResponsibleParties",
                column: "ContactId",
                principalSchema: "tentacion_sana",
                principalTable: "CustomerContacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerContacts_DeliveryPoints_DeliveryPointId",
                schema: "tentacion_sana",
                table: "CustomerContacts");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentResponsibleParties_CustomerContacts_ContactId",
                schema: "tentacion_sana",
                table: "PaymentResponsibleParties");

            migrationBuilder.DropIndex(
                name: "IX_PaymentResponsibleParties_ContactId",
                schema: "tentacion_sana",
                table: "PaymentResponsibleParties");

            migrationBuilder.DropIndex(
                name: "IX_CustomerContacts_DeliveryPointId",
                schema: "tentacion_sana",
                table: "CustomerContacts");

            migrationBuilder.DropColumn(
                name: "ContactId",
                schema: "tentacion_sana",
                table: "PaymentResponsibleParties");

            migrationBuilder.DropColumn(
                name: "Label",
                schema: "tentacion_sana",
                table: "PaymentResponsibleParties");

            migrationBuilder.DropColumn(
                name: "ImageFileName",
                schema: "tentacion_sana",
                table: "DeliveryPoints");

            migrationBuilder.DropColumn(
                name: "ImageFormat",
                schema: "tentacion_sana",
                table: "DeliveryPoints");

            migrationBuilder.DropColumn(
                name: "ImagePublicId",
                schema: "tentacion_sana",
                table: "DeliveryPoints");

            migrationBuilder.DropColumn(
                name: "Notes",
                schema: "tentacion_sana",
                table: "DeliveryPoints");

            migrationBuilder.DropColumn(
                name: "Category",
                schema: "tentacion_sana",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Notes",
                schema: "tentacion_sana",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "DeliveryPointId",
                schema: "tentacion_sana",
                table: "CustomerContacts");

            migrationBuilder.DropColumn(
                name: "IsAccounting",
                schema: "tentacion_sana",
                table: "CustomerContacts");

            migrationBuilder.DropColumn(
                name: "IsAdministrator",
                schema: "tentacion_sana",
                table: "CustomerContacts");

            migrationBuilder.DropColumn(
                name: "IsOrderContact",
                schema: "tentacion_sana",
                table: "CustomerContacts");

            migrationBuilder.DropColumn(
                name: "IsOwner",
                schema: "tentacion_sana",
                table: "CustomerContacts");

            migrationBuilder.DropColumn(
                name: "IsPaymentContact",
                schema: "tentacion_sana",
                table: "CustomerContacts");

            migrationBuilder.DropColumn(
                name: "IsReceptionContact",
                schema: "tentacion_sana",
                table: "CustomerContacts");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                schema: "tentacion_sana",
                table: "Customers",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);
        }
    }
}
