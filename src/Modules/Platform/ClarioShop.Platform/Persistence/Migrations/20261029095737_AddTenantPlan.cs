using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClarioShop.Platform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Shops created before plans existed had every feature; they keep it.
            migrationBuilder.AddColumn<string>(
                name: "Plan",
                schema: "platform",
                table: "Tenants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "RetailSuite");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Plan",
                schema: "platform",
                table: "Tenants");
        }
    }
}
