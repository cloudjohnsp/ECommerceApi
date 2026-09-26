using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "currency",
                table: "payments",
                type: "character(3)",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE payments SET currency = 'BRL' WHERE currency IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "currency",
                table: "payments",
                type: "character(3)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character(3)",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_payments_currency",
                table: "payments",
                sql: "\"currency\" ~ '^[A-Z]{3}$'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_payments_currency",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "currency",
                table: "payments");
        }
    }
}
