using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersistedInventoryReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inventory_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_reservations", x => x.Id);
                    table.CheckConstraint("ck_inventory_reservations_completion", "(status = 1 AND completed_at IS NULL) OR (status <> 1 AND completed_at IS NOT NULL AND completed_at >= created_at)");
                    table.CheckConstraint("ck_inventory_reservations_expiration", "expires_at > created_at");
                    table.CheckConstraint("ck_inventory_reservations_quantity", "quantity > 0");
                    table.CheckConstraint("ck_inventory_reservations_status", "status IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_inventory_reservations_inventories_inventory_id",
                        column: x => x.inventory_id,
                        principalTable: "inventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_reservations_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_reservations_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO inventory_reservations
                    ("Id", order_id, product_id, inventory_id, quantity, status,
                     created_at, expires_at, completed_at)
                SELECT oi."Id", oi.order_id, oi.product_id, i."Id", oi.quantity, 1,
                       o.created_at, o.created_at + INTERVAL '30 minutes', NULL
                FROM order_items AS oi
                INNER JOIN orders AS o ON o."Id" = oi.order_id
                INNER JOIN inventories AS i ON i.product_id = oi.product_id
                WHERE o.status = 1;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_inventory_reservations_active_expires_at",
                table: "inventory_reservations",
                column: "expires_at",
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_reservations_inventory_id",
                table: "inventory_reservations",
                column: "inventory_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_product_id",
                table: "inventory_reservations",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ux_inventory_reservations_order_id_product_id",
                table: "inventory_reservations",
                columns: new[] { "order_id", "product_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_reservations");
        }
    }
}
