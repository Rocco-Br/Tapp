using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tapp.Api.Migrations
{
    /// <inheritdoc />
    public partial class ForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rows orphaned by the old optional relations would break the new NOT NULL foreign keys.
            migrationBuilder.Sql("""
                DELETE FROM "OrderLine" WHERE "OrderId" IS NULL;
                DELETE FROM "Option_Stock" WHERE "OwnerId" IN (SELECT "Id" FROM "Option" WHERE "OptionGroupId" IS NULL
                    OR "OptionGroupId" IN (SELECT "Id" FROM "OptionGroup" WHERE "ProductId" IS NULL));
                DELETE FROM "Option" WHERE "OptionGroupId" IS NULL
                    OR "OptionGroupId" IN (SELECT "Id" FROM "OptionGroup" WHERE "ProductId" IS NULL);
                DELETE FROM "OptionGroup" WHERE "ProductId" IS NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Option_OptionGroup_OptionGroupId",
                table: "Option");

            migrationBuilder.DropForeignKey(
                name: "FK_OptionGroup_Products_ProductId",
                table: "OptionGroup");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderLine_Orders_OrderId",
                table: "OrderLine");

            migrationBuilder.AlterColumn<int>(
                name: "OrderId",
                table: "OrderLine",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ProductId",
                table: "OptionGroup",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OptionGroupId",
                table: "Option",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockLogs_StockItemId",
                table: "StockLogs",
                column: "StockItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_EventId",
                table: "Orders",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_GuestId",
                table: "Orders",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PlacedById",
                table: "Orders",
                column: "PlacedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Option_OptionGroup_OptionGroupId",
                table: "Option",
                column: "OptionGroupId",
                principalTable: "OptionGroup",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OptionGroup_Products_ProductId",
                table: "OptionGroup",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderLine_Orders_OrderId",
                table: "OrderLine",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_AspNetUsers_GuestId",
                table: "Orders",
                column: "GuestId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_AspNetUsers_PlacedById",
                table: "Orders",
                column: "PlacedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Events_EventId",
                table: "Orders",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockLogs_StockItems_StockItemId",
                table: "StockLogs",
                column: "StockItemId",
                principalTable: "StockItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Option_OptionGroup_OptionGroupId",
                table: "Option");

            migrationBuilder.DropForeignKey(
                name: "FK_OptionGroup_Products_ProductId",
                table: "OptionGroup");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderLine_Orders_OrderId",
                table: "OrderLine");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_AspNetUsers_GuestId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_AspNetUsers_PlacedById",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Events_EventId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_StockLogs_StockItems_StockItemId",
                table: "StockLogs");

            migrationBuilder.DropIndex(
                name: "IX_StockLogs_StockItemId",
                table: "StockLogs");

            migrationBuilder.DropIndex(
                name: "IX_Orders_EventId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_GuestId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_PlacedById",
                table: "Orders");

            migrationBuilder.AlterColumn<int>(
                name: "OrderId",
                table: "OrderLine",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "ProductId",
                table: "OptionGroup",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "OptionGroupId",
                table: "Option",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddForeignKey(
                name: "FK_Option_OptionGroup_OptionGroupId",
                table: "Option",
                column: "OptionGroupId",
                principalTable: "OptionGroup",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OptionGroup_Products_ProductId",
                table: "OptionGroup",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderLine_Orders_OrderId",
                table: "OrderLine",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id");
        }
    }
}
