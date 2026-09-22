using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtractInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reserved_stock = table.Column<int>(type: "integer", nullable: false),
                    stock = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventories_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventories_product_id",
                table: "inventories",
                column: "product_id",
                unique: true);

            migrationBuilder.Sql("""
                WITH pending AS (
                    SELECT item.product_id, SUM(item.quantity)::integer AS quantity
                    FROM order_items AS item
                    JOIN orders AS order_record ON order_record."Id" = item.order_id
                    WHERE order_record.status = 1
                    GROUP BY item.product_id
                )
                INSERT INTO inventories ("Id", product_id, reserved_stock, stock)
                SELECT product."Id", product."Id", COALESCE(pending.quantity, 0),
                       product.stock + COALESCE(pending.quantity, 0)
                FROM products AS product
                LEFT JOIN pending ON pending.product_id = product."Id"
                """);

            migrationBuilder.DropColumn(
                name: "stock",
                table: "products");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "stock",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE products SET stock = inventories.stock - inventories.reserved_stock
                FROM inventories WHERE products."Id" = inventories.product_id
                """);

            migrationBuilder.DropTable(
                name: "inventories");
        }
    }
}
