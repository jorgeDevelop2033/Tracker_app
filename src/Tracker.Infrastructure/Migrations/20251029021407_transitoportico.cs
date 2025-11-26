using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Tracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class transitoportico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransitosPortico",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PorticoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GpsPunto = table.Column<Point>(type: "geography", nullable: false),
                    VelocidadKmh = table.Column<double>(type: "float", nullable: true),
                    HeadingGrados = table.Column<double>(type: "float", nullable: true),
                    DistanciaMetros = table.Column<double>(type: "float", nullable: false),
                    SourceDeviceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RawId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Via = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Sentido = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    rowversion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransitosPortico", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransitosPortico_Porticos_PorticoId",
                        column: x => x.PorticoId,
                        principalSchema: "tracker",
                        principalTable: "Porticos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransitosPortico_PorticoId_TimestampUtc",
                schema: "tracker",
                table: "TransitosPortico",
                columns: new[] { "PorticoId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TransitosPortico_RawId",
                schema: "tracker",
                table: "TransitosPortico",
                column: "RawId");

            migrationBuilder.CreateIndex(
                name: "IX_TransitosPortico_TimestampUtc",
                schema: "tracker",
                table: "TransitosPortico",
                column: "TimestampUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransitosPortico",
                schema: "tracker");
        }
    }
}
