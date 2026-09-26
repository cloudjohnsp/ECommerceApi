using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDomainCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_products_price",
                table: "products",
                sql: "price > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_payments_amount",
                table: "payments",
                sql: "amount > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_payments_status",
                table: "payments",
                sql: "status IN (1, 2, 3, 4)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_status",
                table: "orders",
                sql: "status IN (1, 2, 3, 4)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_order_items_quantity",
                table: "order_items",
                sql: "quantity > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_order_items_unit_price",
                table: "order_items",
                sql: "unit_price > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_inventories_reservation_capacity",
                table: "inventories",
                sql: "reserved_stock <= stock");

            migrationBuilder.AddCheckConstraint(
                name: "ck_inventories_reserved_stock",
                table: "inventories",
                sql: "reserved_stock >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_inventories_stock",
                table: "inventories",
                sql: "stock >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_products_price",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "ck_payments_amount",
                table: "payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_payments_status",
                table: "payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_status",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_order_items_quantity",
                table: "order_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_order_items_unit_price",
                table: "order_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_inventories_reservation_capacity",
                table: "inventories");

            migrationBuilder.DropCheckConstraint(
                name: "ck_inventories_reserved_stock",
                table: "inventories");

            migrationBuilder.DropCheckConstraint(
                name: "ck_inventories_stock",
                table: "inventories");
        }
    }
}
