using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClarioShop.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderCharges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryFee",
                schema: "ordering",
                table: "Orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "DeliveryZoneId",
                schema: "ordering",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryZoneName",
                schema: "ordering",
                table: "Orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Vat",
                schema: "ordering",
                table: "Orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatRatePercent",
                schema: "ordering",
                table: "Orders",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryFee",
                schema: "ordering",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryZoneId",
                schema: "ordering",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryZoneName",
                schema: "ordering",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Vat",
                schema: "ordering",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "VatRatePercent",
                schema: "ordering",
                table: "Orders");
        }
    }
}
