using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Tracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialUpdateEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tracker");

            migrationBuilder.CreateTable(
                name: "gps_fix",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    device_id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    lat = table.Column<double>(type: "float", nullable: false),
                    lon = table.Column<double>(type: "float", nullable: false),
                    speed_kph = table.Column<double>(type: "float", nullable: true),
                    heading_deg = table.Column<double>(type: "float", nullable: true),
                    accuracy_m = table.Column<double>(type: "float", nullable: true),
                    utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    location = table.Column<Point>(type: "geography", nullable: false),
                    kafka_topic = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    kafka_partition = table.Column<int>(type: "int", nullable: false),
                    kafka_offset = table.Column<long>(type: "bigint", nullable: false),
                    rowversion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gps_fix", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Porticos",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Autopista = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Sentido = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CallesRef = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LongitudKm = table.Column<decimal>(type: "decimal(6,3)", nullable: true),
                    Ubicacion = table.Column<Point>(type: "geography", nullable: true),
                    Corredor = table.Column<LineString>(type: "geography", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false),
                    rowversion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Porticos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TarifasPortico",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PorticoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Banda = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    ValorFijo = table.Column<decimal>(type: "decimal(12,2)", nullable: true),
                    ValorPorKm = table.Column<decimal>(type: "decimal(12,6)", nullable: true),
                    LongitudKmSnapshot = table.Column<decimal>(type: "decimal(12,6)", nullable: true),
                    VigenteDesde = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VigenteHasta = table.Column<DateTime>(type: "datetime2", nullable: true),
                    rowversion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TarifasPortico", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TarifasPortico_Porticos_PorticoId",
                        column: x => x.PorticoId,
                        principalSchema: "tracker",
                        principalTable: "Porticos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Transitos",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PorticoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Banda = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    PrecioCalculado = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Posicion = table.Column<Point>(type: "geography", nullable: true),
                    ExactitudM = table.Column<double>(type: "float", nullable: true),
                    Fuente = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    rowversion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transitos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transitos_Portico",
                        column: x => x.PorticoId,
                        principalSchema: "tracker",
                        principalTable: "Porticos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_gpsfix_device_utc",
                schema: "tracker",
                table: "gps_fix",
                columns: new[] { "device_id", "utc" });

            migrationBuilder.CreateIndex(
                name: "ux_gpsfix_kafka_position",
                schema: "tracker",
                table: "gps_fix",
                columns: new[] { "kafka_topic", "kafka_partition", "kafka_offset" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Porticos_Autopista_Codigo_Sentido",
                schema: "tracker",
                table: "Porticos",
                columns: new[] { "Autopista", "Codigo", "Sentido" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tarifa_Banda_Desde",
                schema: "tracker",
                table: "TarifasPortico",
                columns: new[] { "PorticoId", "Banda", "VigenteDesde" });

            migrationBuilder.CreateIndex(
                name: "IX_Tarifa_Vigencias",
                schema: "tracker",
                table: "TarifasPortico",
                columns: new[] { "PorticoId", "Categoria", "Banda", "VigenteDesde", "VigenteHasta" });

            migrationBuilder.CreateIndex(
                name: "IX_Transitos_Portico_Utc",
                schema: "tracker",
                table: "Transitos",
                columns: new[] { "PorticoId", "Utc" });

            migrationBuilder.CreateIndex(
                name: "IX_Transitos_Utc",
                schema: "tracker",
                table: "Transitos",
                column: "Utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gps_fix",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "TarifasPortico",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "Transitos",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "Porticos",
                schema: "tracker");
        }
    }
}
