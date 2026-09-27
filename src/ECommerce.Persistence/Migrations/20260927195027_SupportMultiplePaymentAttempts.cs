using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupportMultiplePaymentAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payments_order_id",
                table: "payments");

            migrationBuilder.AddColumn<string>(
                name: "idempotency_key",
                table: "payments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE payments
                SET idempotency_key = 'legacy:' || "Id"::text;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "idempotency_key",
                table: "payments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_payments_one_paid_per_order",
                table: "payments",
                column: "order_id",
                unique: true,
                filter: "status = 2");

            migrationBuilder.CreateIndex(
                name: "ux_payments_one_pending_per_order",
                table: "payments",
                column: "order_id",
                unique: true,
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "ux_payments_order_id_idempotency_key",
                table: "payments",
                columns: new[] { "order_id", "idempotency_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_payments_one_paid_per_order",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "ux_payments_one_pending_per_order",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "ux_payments_order_id_idempotency_key",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "idempotency_key",
                table: "payments");

            migrationBuilder.CreateIndex(
                name: "IX_payments_order_id",
                table: "payments",
                column: "order_id",
                unique: true);
        }
    }
}
