using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClarioShop.Payments.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGatewayPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankTransactionId",
                schema: "payments",
                table: "PaymentAttempts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardType",
                schema: "payments",
                table: "PaymentAttempts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                schema: "payments",
                table: "PaymentAttempts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SessionKey",
                schema: "payments",
                table: "PaymentAttempts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidationId",
                schema: "payments",
                table: "PaymentAttempts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // Cash attempts recorded before this column existed completed when they were recorded.
            migrationBuilder.Sql("""UPDATE payments."PaymentAttempts" SET "CompletedAt" = "AttemptedAt" WHERE "Outcome" <> 'Pending'""");

            migrationBuilder.CreateTable(
                name: "GatewayWebhookLogs",
                schema: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Gateway = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    TransactionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ValidationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReportedStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatewayWebhookLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GatewayWebhookLogs_ReceivedAt",
                schema: "payments",
                table: "GatewayWebhookLogs",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GatewayWebhookLogs_TransactionId",
                schema: "payments",
                table: "GatewayWebhookLogs",
                column: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GatewayWebhookLogs",
                schema: "payments");

            migrationBuilder.DropColumn(
                name: "BankTransactionId",
                schema: "payments",
                table: "PaymentAttempts");

            migrationBuilder.DropColumn(
                name: "CardType",
                schema: "payments",
                table: "PaymentAttempts");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                schema: "payments",
                table: "PaymentAttempts");

            migrationBuilder.DropColumn(
                name: "SessionKey",
                schema: "payments",
                table: "PaymentAttempts");

            migrationBuilder.DropColumn(
                name: "ValidationId",
                schema: "payments",
                table: "PaymentAttempts");
        }
    }
}
