using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Infrastructure.Migrations
{
    /// <summary>
    /// Índices espaciales de Porticos y Transitos.
    /// </summary>
    /// <remarks>
    /// Vivían en Scripts/CreateSpatialIndexes.sql como paso manual y nunca se
    /// aplicaron en producción: el script apuntaba a dbo.Porticos, pero el
    /// schema real es tracker. Sin ellos, el GetNearAsync del detector hace
    /// scan completo de la tabla calculando STDistance fila por fila —dos
    /// veces, para el WHERE y para el ORDER BY— en CADA fix GPS, lo que
    /// saturaba el pool de conexiones y devolvía "Timeout expired" al cliente.
    ///
    /// EF Core no modela índices espaciales, así que van como SQL crudo.
    /// </remarks>
    public partial class IndicesEspaciales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // GEOGRAPHY_GRID es el único esquema válido para columnas
            // geography. CELLS_PER_OBJECT=16 es el default razonable para
            // puntos; el corredor es un LineString y también se conforma.
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes
                               WHERE name = 'IX_Porticos_Ubicacion'
                                 AND object_id = OBJECT_ID('tracker.Porticos'))
                    CREATE SPATIAL INDEX IX_Porticos_Ubicacion
                    ON tracker.Porticos(Ubicacion)
                    USING GEOGRAPHY_GRID
                    WITH (GRIDS = (MEDIUM, MEDIUM, MEDIUM, MEDIUM), CELLS_PER_OBJECT = 16);
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes
                               WHERE name = 'IX_Porticos_Corredor'
                                 AND object_id = OBJECT_ID('tracker.Porticos'))
                    CREATE SPATIAL INDEX IX_Porticos_Corredor
                    ON tracker.Porticos(Corredor)
                    USING GEOGRAPHY_GRID
                    WITH (GRIDS = (MEDIUM, MEDIUM, MEDIUM, MEDIUM), CELLS_PER_OBJECT = 16);
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes
                               WHERE name = 'IX_Transitos_Posicion'
                                 AND object_id = OBJECT_ID('tracker.Transitos'))
                    CREATE SPATIAL INDEX IX_Transitos_Posicion
                    ON tracker.Transitos(Posicion)
                    USING GEOGRAPHY_GRID
                    WITH (GRIDS = (MEDIUM, MEDIUM, MEDIUM, MEDIUM), CELLS_PER_OBJECT = 16);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Transitos_Posicion ON tracker.Transitos;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Porticos_Corredor ON tracker.Porticos;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Porticos_Ubicacion ON tracker.Porticos;");
        }
    }
}
