using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Tracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Porticos",
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
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Porticos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TarifasPortico",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PorticoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    Banda = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    ValorFijo = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    ValorPorKm = table.Column<decimal>(type: "decimal(8,3)", nullable: true),
                    LongitudKmSnapshot = table.Column<decimal>(type: "decimal(6,3)", nullable: true),
                    VigenteDesde = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VigenteHasta = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TarifasPortico", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TarifasPortico_Porticos_PorticoId",
                        column: x => x.PorticoId,
                        principalTable: "Porticos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Transitos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PorticoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Banda = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    PrecioCalculado = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    Posicion = table.Column<Point>(type: "geography", nullable: true),
                    ExactitudM = table.Column<double>(type: "float", nullable: true),
                    Fuente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transitos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transitos_Porticos_PorticoId",
                        column: x => x.PorticoId,
                        principalTable: "Porticos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Porticos_Autopista_Codigo_Sentido",
                table: "Porticos",
                columns: new[] { "Autopista", "Codigo", "Sentido" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TarifasPortico_PorticoId_Banda_VigenteDesde",
                table: "TarifasPortico",
                columns: new[] { "PorticoId", "Banda", "VigenteDesde" });

            migrationBuilder.CreateIndex(
                name: "IX_TarifasPortico_PorticoId_Categoria_Banda_VigenteDesde_VigenteHasta",
                table: "TarifasPortico",
                columns: new[] { "PorticoId", "Categoria", "Banda", "VigenteDesde", "VigenteHasta" });

            migrationBuilder.CreateIndex(
                name: "IX_Transitos_PorticoId_Utc",
                table: "Transitos",
                columns: new[] { "PorticoId", "Utc" });

            migrationBuilder.CreateIndex(
                name: "IX_Transitos_Utc",
                table: "Transitos",
                column: "Utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TarifasPortico");

            migrationBuilder.DropTable(
                name: "Transitos");

            migrationBuilder.DropTable(
                name: "Porticos");
        }
    }
}
