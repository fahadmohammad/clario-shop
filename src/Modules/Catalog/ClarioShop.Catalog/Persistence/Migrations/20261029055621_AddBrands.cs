using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClarioShop.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBrands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BrandId",
                schema: "catalog",
                table: "Products",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Brands",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brands", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_BrandId",
                schema: "catalog",
                table: "Products",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Status_BrandId",
                schema: "catalog",
                table: "Products",
                columns: new[] { "Status", "BrandId" });

            migrationBuilder.CreateIndex(
                name: "IX_Brands_NormalizedName",
                schema: "catalog",
                table: "Brands",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Brands_Slug",
                schema: "catalog",
                table: "Brands",
                column: "Slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Brands_BrandId",
                schema: "catalog",
                table: "Products",
                column: "BrandId",
                principalSchema: "catalog",
                principalTable: "Brands",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Turn the free-text brands into Brand rows, one per name ignoring case (spelled as on its oldest product),
            // and link their products.
            // Slugs follow Slug.From; a name with no Latin letters or digits, or one whose slug another name
            // already has, gets a suffix from its hash.
            migrationBuilder.Sql("""
                WITH names AS (
                    SELECT upper(btrim("Brand")) AS normalized, (array_agg(btrim("Brand") ORDER BY "CreatedAt", "Id"))[1] AS name, min("CreatedAt") AS created_at
                    FROM catalog."Products"
                    WHERE btrim(coalesce("Brand", '')) <> ''
                    GROUP BY upper(btrim("Brand"))
                ),
                slugged AS (
                    SELECT *, btrim(left(btrim(regexp_replace(lower(name), '[^a-z0-9]+', '-', 'g'), '-'), 120), '-') AS base
                    FROM names
                ),
                ranked AS (
                    SELECT *, row_number() OVER (PARTITION BY base ORDER BY normalized) AS position
                    FROM slugged
                )
                INSERT INTO catalog."Brands" ("Id", "Name", "NormalizedName", "Slug", "CreatedAt")
                SELECT gen_random_uuid(), name, normalized,
                       CASE
                           WHEN base = '' THEN 'brand-' || left(md5(normalized), 8)
                           WHEN position > 1 THEN base || '-' || left(md5(normalized), 8)
                           ELSE base
                       END,
                       created_at
                FROM ranked;

                UPDATE catalog."Products" AS product
                SET "BrandId" = brand."Id"
                FROM catalog."Brands" AS brand
                WHERE upper(btrim(product."Brand")) = brand."NormalizedName";
                """);

            migrationBuilder.DropColumn(
                name: "Brand",
                schema: "catalog",
                table: "Products");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Brand",
                schema: "catalog",
                table: "Products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE catalog."Products" AS product
                SET "Brand" = brand."Name"
                FROM catalog."Brands" AS brand
                WHERE product."BrandId" = brand."Id";
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Brands_BrandId",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropTable(
                name: "Brands",
                schema: "catalog");

            migrationBuilder.DropIndex(
                name: "IX_Products_BrandId",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Status_BrandId",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "BrandId",
                schema: "catalog",
                table: "Products");
        }
    }
}
