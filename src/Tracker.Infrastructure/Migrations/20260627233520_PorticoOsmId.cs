using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PorticoOsmId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Porticos_Autopista_Codigo_Sentido",
                schema: "tracker",
                table: "Porticos");

            migrationBuilder.AlterColumn<string>(
                name: "Sentido",
                schema: "tracker",
                table: "Porticos",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "Codigo",
                schema: "tracker",
                table: "Porticos",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AddColumn<long>(
                name: "OsmId",
                schema: "tracker",
                table: "Porticos",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Porticos_Autopista_Codigo_Sentido",
                schema: "tracker",
                table: "Porticos",
                columns: new[] { "Autopista", "Codigo", "Sentido" });

            migrationBuilder.CreateIndex(
                name: "IX_Porticos_OsmId",
                schema: "tracker",
                table: "Porticos",
                column: "OsmId",
                unique: true,
                filter: "[OsmId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Porticos_Autopista_Codigo_Sentido",
                schema: "tracker",
                table: "Porticos");

            migrationBuilder.DropIndex(
                name: "IX_Porticos_OsmId",
                schema: "tracker",
                table: "Porticos");

            migrationBuilder.DropColumn(
                name: "OsmId",
                schema: "tracker",
                table: "Porticos");

            migrationBuilder.AlterColumn<string>(
                name: "Sentido",
                schema: "tracker",
                table: "Porticos",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(60)",
                oldMaxLength: 60);

            migrationBuilder.AlterColumn<string>(
                name: "Codigo",
                schema: "tracker",
                table: "Porticos",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "IX_Porticos_Autopista_Codigo_Sentido",
                schema: "tracker",
                table: "Porticos",
                columns: new[] { "Autopista", "Codigo", "Sentido" },
                unique: true);
        }
    }
}
