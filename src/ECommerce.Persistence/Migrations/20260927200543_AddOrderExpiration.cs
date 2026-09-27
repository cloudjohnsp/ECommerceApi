using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderExpiration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "cancellation_reason",
                table: "orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "expires_at",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE orders AS o
                SET expires_at = COALESCE(
                        (SELECT MIN(r.expires_at)
                         FROM inventory_reservations AS r
                         WHERE r.order_id = o."Id"),
                        o.created_at + INTERVAL '30 minutes'),
                    cancellation_reason = CASE WHEN o.status = 3 THEN 1 ELSE NULL END;
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "expires_at",
                table: "orders",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_pending_expires_at",
                table: "orders",
                column: "expires_at",
                filter: "status = 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_cancellation_reason",
                table: "orders",
                sql: "(status = 3 AND cancellation_reason IS NOT NULL) OR (status <> 3 AND cancellation_reason IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_expiration",
                table: "orders",
                sql: "expires_at > created_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_orders_pending_expires_at",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_cancellation_reason",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_expiration",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "cancellation_reason",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "expires_at",
                table: "orders");
        }
    }
}
