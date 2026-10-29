using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClarioShop.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogEditing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "catalog",
                table: "Variants",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                schema: "catalog",
                table: "Products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "catalog",
                table: "Products",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "AuditLog",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Before = table.Column<string>(type: "jsonb", nullable: true),
                    After = table.Column<string>(type: "jsonb", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Device = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_AuditLog_Entity",
                schema: "catalog",
                table: "AuditLog",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_AuditLog_OccurredAt",
                schema: "catalog",
                table: "AuditLog",
                column: "OccurredAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLog",
                schema: "catalog");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "catalog",
                table: "Variants");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "catalog",
                table: "Products");
        }
    }
}
