using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class transito_v : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transitos_Portico",
                schema: "tracker",
                table: "Transitos");

            migrationBuilder.RenameIndex(
                name: "IX_Transitos_Portico_Utc",
                schema: "tracker",
                table: "Transitos",
                newName: "IX_Transitos_PorticoId_Utc");

            migrationBuilder.AlterColumn<decimal>(
                name: "PrecioCalculado",
                schema: "tracker",
                table: "Transitos",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)");

            migrationBuilder.AlterColumn<string>(
                name: "Fuente",
                schema: "tracker",
                table: "Transitos",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "GPS",
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<int>(
                name: "Categoria",
                schema: "tracker",
                table: "Transitos",
                type: "int",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "Banda",
                schema: "tracker",
                table: "Transitos",
                type: "int",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16);

            migrationBuilder.CreateIndex(
                name: "IX_Transitos_PorticoId",
                schema: "tracker",
                table: "Transitos",
                column: "PorticoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transitos_Porticos_PorticoId",
                schema: "tracker",
                table: "Transitos",
                column: "PorticoId",
                principalSchema: "tracker",
                principalTable: "Porticos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transitos_Porticos_PorticoId",
                schema: "tracker",
                table: "Transitos");

            migrationBuilder.DropIndex(
                name: "IX_Transitos_PorticoId",
                schema: "tracker",
                table: "Transitos");

            migrationBuilder.RenameIndex(
                name: "IX_Transitos_PorticoId_Utc",
                schema: "tracker",
                table: "Transitos",
                newName: "IX_Transitos_Portico_Utc");

            migrationBuilder.AlterColumn<decimal>(
                name: "PrecioCalculado",
                schema: "tracker",
                table: "Transitos",
                type: "decimal(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldPrecision: 18,
                oldScale: 4,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<string>(
                name: "Fuente",
                schema: "tracker",
                table: "Transitos",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldDefaultValue: "GPS");

            migrationBuilder.AlterColumn<int>(
                name: "Categoria",
                schema: "tracker",
                table: "Transitos",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 1);

            migrationBuilder.AlterColumn<string>(
                name: "Banda",
                schema: "tracker",
                table: "Transitos",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 1);

            migrationBuilder.AddForeignKey(
                name: "FK_Transitos_Portico",
                schema: "tracker",
                table: "Transitos",
                column: "PorticoId",
                principalSchema: "tracker",
                principalTable: "Porticos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
