using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClarioShop.Inventory.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReorderLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ReorderLevel",
                schema: "inventory",
                table: "Items",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReorderLevel",
                schema: "inventory",
                table: "Items");
        }
    }
}
