using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClarioShop.Settings.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "settings");

            migrationBuilder.CreateTable(
                name: "DeliveryZones",
                schema: "settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Areas = table.Column<List<string>>(type: "text[]", nullable: false),
                    Fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FreeDeliveryThreshold = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryZones", x => x.Id);
                    table.CheckConstraint("CK_DeliveryZones_Fee", "\"Fee\" >= 0");
                    table.CheckConstraint("CK_DeliveryZones_FreeDeliveryThreshold", "\"FreeDeliveryThreshold\" IS NULL OR \"FreeDeliveryThreshold\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "InboxMessages",
                schema: "settings",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Handler = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => new { x.MessageId, x.Handler });
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Content = table.Column<string>(type: "jsonb", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoreSettings",
                schema: "settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoicePrefix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    VatRatePercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    DefaultLanguage = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Languages = table.Column<List<string>>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Currency_DecimalDigits = table.Column<int>(type: "integer", nullable: false),
                    Currency_Grouping = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Currency_Symbol = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Currency_SymbolPosition = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Profile_AddressLine1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Profile_AddressLine2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Profile_Area = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Profile_City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Profile_ContactEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    Profile_ContactPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Profile_PostalCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Profile_StoreName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Theme_AccentColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Theme_BackgroundColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Theme_BodyFont = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Theme_FaviconUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Theme_ForegroundColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Theme_HeadingFont = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Theme_LogoUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Theme_PrimaryColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Theme_PrimaryForegroundColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Theme_RadiusRem = table.Column<decimal>(type: "numeric(4,3)", precision: 4, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreSettings", x => x.Id);
                    table.CheckConstraint("CK_StoreSettings_Singleton", "\"Id\" = '01000000-0000-7000-8000-000000000001'");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryZones_NormalizedName",
                schema: "settings",
                table: "DeliveryZones",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_settings_OutboxMessages_Unprocessed",
                schema: "settings",
                table: "OutboxMessages",
                column: "OccurredAt",
                filter: "\"ProcessedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeliveryZones",
                schema: "settings");

            migrationBuilder.DropTable(
                name: "InboxMessages",
                schema: "settings");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "settings");

            migrationBuilder.DropTable(
                name: "StoreSettings",
                schema: "settings");
        }
    }
}
