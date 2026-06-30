using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TarifasBandaHorarioYDeviceTransito : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeviceId",
                schema: "tracker",
                table: "Transitos",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BandasHorario",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PorticoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiaTipo = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    HoraInicio = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    HoraFin = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    Banda = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    rowversion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BandasHorario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BandasHorario_Porticos_PorticoId",
                        column: x => x.PorticoId,
                        principalSchema: "tracker",
                        principalTable: "Porticos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transitos_DeviceId_Utc",
                schema: "tracker",
                table: "Transitos",
                columns: new[] { "DeviceId", "Utc" });

            migrationBuilder.CreateIndex(
                name: "IX_BandaHorario_Portico_Dia",
                schema: "tracker",
                table: "BandasHorario",
                columns: new[] { "PorticoId", "DiaTipo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BandasHorario",
                schema: "tracker");

            migrationBuilder.DropIndex(
                name: "IX_Transitos_DeviceId_Utc",
                schema: "tracker",
                table: "Transitos");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                schema: "tracker",
                table: "Transitos");
        }
    }
}
