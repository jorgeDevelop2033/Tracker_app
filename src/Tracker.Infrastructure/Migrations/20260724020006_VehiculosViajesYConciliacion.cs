using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Tracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VehiculosViajesYConciliacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransitosPortico",
                schema: "tracker");

            migrationBuilder.AddColumn<string>(
                name: "AutopistaSnapshot",
                schema: "tracker",
                table: "Transitos",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiaTipo",
                schema: "tracker",
                table: "Transitos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EstadoConciliacion",
                schema: "tracker",
                table: "Transitos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "FechaLocal",
                schema: "tracker",
                table: "Transitos",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "HoraLocal",
                schema: "tracker",
                table: "Transitos",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddColumn<string>(
                name: "PorticoCodigoSnapshot",
                schema: "tracker",
                table: "Transitos",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SentidoSnapshot",
                schema: "tracker",
                table: "Transitos",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TarifaPorticoId",
                schema: "tracker",
                table: "Transitos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VehiculoId",
                schema: "tracker",
                table: "Transitos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ViajeId",
                schema: "tracker",
                table: "Transitos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "viaje_id",
                schema: "tracker",
                table: "gps_fix",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Vehiculos",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Patente = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Alias = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    Marca = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Modelo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Anio = table.Column<int>(type: "int", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    rowversion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehiculos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AsignacionesDispositivo",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    VehiculoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DesdeUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HastaUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Nota = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    rowversion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsignacionesDispositivo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AsignacionesDispositivo_Vehiculos_VehiculoId",
                        column: x => x.VehiculoId,
                        principalSchema: "tracker",
                        principalTable: "Vehiculos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Viajes",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehiculoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    InicioUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaLocalInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    PuntoInicio = table.Column<Point>(type: "geography", nullable: true),
                    PuntoFin = table.Column<Point>(type: "geography", nullable: true),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Nota = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CantidadTransitos = table.Column<int>(type: "int", nullable: false),
                    TotalGasto = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    DistanciaKm = table.Column<double>(type: "float", nullable: false),
                    CantidadFixes = table.Column<int>(type: "int", nullable: false),
                    RutaSimplificada = table.Column<LineString>(type: "geography", nullable: true),
                    rowversion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Viajes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Viajes_Vehiculos_VehiculoId",
                        column: x => x.VehiculoId,
                        principalSchema: "tracker",
                        principalTable: "Vehiculos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // =================================================================
            //  BACKFILL de los tránsitos ya existentes
            // =================================================================
            // Las columnas nuevas nacen con default (FechaLocal = 0001-01-01),
            // así que sin esto todo el historial previo quedaría fuera de
            // cualquier reporte por fecha. Se rellenan desde los datos que ya
            // están en la fila.

            // 1) Fecha y hora locales de Chile a partir del instante UTC.
            //    'Pacific SA Standard Time' es el id de Chile que entiende
            //    SQL Server (también en Linux) e incluye horario de verano.
            migrationBuilder.Sql(@"
                UPDATE t
                SET FechaLocal = CAST(l.LocalDt AS date),
                    HoraLocal  = CAST(l.LocalDt AS time(0))
                FROM tracker.Transitos AS t
                CROSS APPLY (
                    SELECT CAST(t.Utc AT TIME ZONE 'UTC'
                                      AT TIME ZONE 'Pacific SA Standard Time' AS datetime2(0)) AS LocalDt
                ) AS l;
            ");

            // 2) Tipo de día a partir del día de la semana local.
            //    OJO: los FESTIVOS no se pueden derivar en SQL, así que un
            //    tránsito histórico caído en feriado queda marcado Laboral o
            //    Sábado. Es solo un campo de auditoría añadido ahora; la Banda
            //    de esas filas ya se calculó en su momento con el calendario
            //    real y no se toca. Los tránsitos nuevos sí traen el DiaTipo
            //    correcto desde el detector.
            //    0 = Laboral, 1 = Sábado, 2 = Domingo/Festivo
            //
            //    No se usa DATEPART(WEEKDAY): depende de @@DATEFIRST, que cambia
            //    con el idioma del login y daría un resultado distinto según el
            //    servidor donde se aplique la migración. DATEDIFF contra un
            //    domingo conocido (1905-01-01) es independiente de esa config.
            migrationBuilder.Sql(@"
                UPDATE tracker.Transitos
                SET DiaTipo = CASE DATEDIFF(day, '19050101', FechaLocal) % 7
                                WHEN 0 THEN 2   -- domingo
                                WHEN 6 THEN 1   -- sábado
                                ELSE 0          -- laboral
                              END
                WHERE FechaLocal > '0001-01-01';
            ");

            // 3) Snapshots del catálogo desde el pórtico actual. Es lo mejor
            //    disponible retroactivamente: refleja la etiqueta de HOY, no la
            //    que tenía el pórtico cuando se pasó. De aquí en adelante el
            //    detector congela el valor correcto en cada tránsito.
            migrationBuilder.Sql(@"
                UPDATE t
                SET AutopistaSnapshot     = p.Autopista,
                    PorticoCodigoSnapshot = p.Codigo,
                    SentidoSnapshot       = p.Sentido
                FROM tracker.Transitos AS t
                INNER JOIN tracker.Porticos AS p ON p.Id = t.PorticoId;
            ");

            // 4) Deduplicado previo al índice único.
            //    Dos filas con el mismo device, el mismo pórtico y EXACTAMENTE
            //    el mismo instante son el mismo paso ingerido dos veces (no
            //    existe forma física de pasar dos veces en el mismo timestamp):
            //    son cobros duplicados. Sin esto, la creación del índice único
            //    falla y la migración no aplica.
            //
            //    NO se borran de forma definitiva: se archivan primero en
            //    tracker.TransitosDuplicadosRespaldo con OUTPUT ... INTO, en la
            //    misma transacción. Esta migración corre desatendida al arrancar
            //    la API, así que el borrado tiene que ser reversible sin restaurar
            //    un backup completo. Si el conteo no cuadra, se recuperan de ahí.
            //    La tabla se puede vaciar a mano una vez revisada.
            //    La tabla se declara con CREATE TABLE explícito y SIN la columna
            //    rowversion. No se usa SELECT ... INTO: al copiar así, SQL Server
            //    hereda el tipo `timestamp` en la tabla destino, y en una columna
            //    timestamp no se puede insertar un valor explícito (error 273),
            //    que es justo con lo que falló el primer intento.
            migrationBuilder.Sql(@"
                IF OBJECT_ID('tracker.TransitosDuplicadosRespaldo', 'U') IS NULL
                BEGIN
                    CREATE TABLE tracker.TransitosDuplicadosRespaldo (
                        Id                    uniqueidentifier NOT NULL,
                        PorticoId             uniqueidentifier NOT NULL,
                        DeviceId              nvarchar(128)    NULL,
                        VehiculoId            uniqueidentifier NULL,
                        ViajeId               uniqueidentifier NULL,
                        Utc                   datetime2        NOT NULL,
                        FechaLocal            date             NOT NULL,
                        HoraLocal             time             NOT NULL,
                        DiaTipo               int              NOT NULL,
                        Banda                 int              NOT NULL,
                        Categoria             int              NOT NULL,
                        PrecioCalculado       decimal(18,4)    NOT NULL,
                        TarifaPorticoId       uniqueidentifier NULL,
                        AutopistaSnapshot     nvarchar(120)    NULL,
                        PorticoCodigoSnapshot nvarchar(40)     NULL,
                        SentidoSnapshot       nvarchar(80)     NULL,
                        EstadoConciliacion    int              NOT NULL,
                        Posicion              geography        NULL,
                        ExactitudM            float            NULL,
                        Fuente                nvarchar(32)     NOT NULL,
                        ArchivadoUtc          datetime2        NOT NULL
                    );
                END;
            ");

            //    El OUTPUT lleva lista explícita de columnas en ambos lados, para
            //    no depender del orden físico de la tabla.
            migrationBuilder.Sql(@"
                WITH duplicados AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY DeviceId, PorticoId, Utc
                               ORDER BY Id) AS rn
                    FROM tracker.Transitos
                    WHERE DeviceId IS NOT NULL
                )
                DELETE t
                OUTPUT deleted.Id, deleted.PorticoId, deleted.DeviceId,
                       deleted.VehiculoId, deleted.ViajeId, deleted.Utc,
                       deleted.FechaLocal, deleted.HoraLocal, deleted.DiaTipo,
                       deleted.Banda, deleted.Categoria, deleted.PrecioCalculado,
                       deleted.TarifaPorticoId, deleted.AutopistaSnapshot,
                       deleted.PorticoCodigoSnapshot, deleted.SentidoSnapshot,
                       deleted.EstadoConciliacion, deleted.Posicion,
                       deleted.ExactitudM, deleted.Fuente, SYSUTCDATETIME()
                    INTO tracker.TransitosDuplicadosRespaldo (
                       Id, PorticoId, DeviceId, VehiculoId, ViajeId, Utc,
                       FechaLocal, HoraLocal, DiaTipo, Banda, Categoria,
                       PrecioCalculado, TarifaPorticoId, AutopistaSnapshot,
                       PorticoCodigoSnapshot, SentidoSnapshot, EstadoConciliacion,
                       Posicion, ExactitudM, Fuente, ArchivadoUtc)
                FROM tracker.Transitos AS t
                INNER JOIN duplicados AS d ON d.Id = t.Id
                WHERE d.rn > 1;
            ");

            migrationBuilder.CreateIndex(
                name: "ix_transito_vehiculo_autopista_fecha",
                schema: "tracker",
                table: "Transitos",
                columns: new[] { "VehiculoId", "AutopistaSnapshot", "FechaLocal" });

            migrationBuilder.CreateIndex(
                name: "ix_transito_vehiculo_fechalocal",
                schema: "tracker",
                table: "Transitos",
                columns: new[] { "VehiculoId", "FechaLocal" });

            migrationBuilder.CreateIndex(
                name: "ix_transito_viaje",
                schema: "tracker",
                table: "Transitos",
                column: "ViajeId");

            migrationBuilder.CreateIndex(
                name: "IX_Transitos_TarifaPorticoId",
                schema: "tracker",
                table: "Transitos",
                column: "TarifaPorticoId");

            migrationBuilder.CreateIndex(
                name: "ux_transito_device_portico_utc",
                schema: "tracker",
                table: "Transitos",
                columns: new[] { "DeviceId", "PorticoId", "Utc" },
                unique: true,
                filter: "[DeviceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_gpsfix_viaje_utc",
                schema: "tracker",
                table: "gps_fix",
                columns: new[] { "viaje_id", "utc" },
                filter: "[viaje_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_asignacion_device_desde",
                schema: "tracker",
                table: "AsignacionesDispositivo",
                columns: new[] { "DeviceId", "DesdeUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_asignacion_vehiculo",
                schema: "tracker",
                table: "AsignacionesDispositivo",
                column: "VehiculoId");

            migrationBuilder.CreateIndex(
                name: "ux_asignacion_device_abierta",
                schema: "tracker",
                table: "AsignacionesDispositivo",
                column: "DeviceId",
                unique: true,
                filter: "[HastaUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_vehiculo_patente",
                schema: "tracker",
                table: "Vehiculos",
                column: "Patente",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_viaje_abierto_device",
                schema: "tracker",
                table: "Viajes",
                columns: new[] { "DeviceId", "Estado" },
                filter: "[Estado] = 0");

            migrationBuilder.CreateIndex(
                name: "ix_viaje_vehiculo_fechalocal",
                schema: "tracker",
                table: "Viajes",
                columns: new[] { "VehiculoId", "FechaLocalInicio" });

            migrationBuilder.CreateIndex(
                name: "ix_viaje_vehiculo_inicio",
                schema: "tracker",
                table: "Viajes",
                columns: new[] { "VehiculoId", "InicioUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_Transitos_TarifasPortico_TarifaPorticoId",
                schema: "tracker",
                table: "Transitos",
                column: "TarifaPorticoId",
                principalSchema: "tracker",
                principalTable: "TarifasPortico",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transitos_Vehiculos_VehiculoId",
                schema: "tracker",
                table: "Transitos",
                column: "VehiculoId",
                principalSchema: "tracker",
                principalTable: "Vehiculos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transitos_Viajes_ViajeId",
                schema: "tracker",
                table: "Transitos",
                column: "ViajeId",
                principalSchema: "tracker",
                principalTable: "Viajes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transitos_TarifasPortico_TarifaPorticoId",
                schema: "tracker",
                table: "Transitos");

            migrationBuilder.DropForeignKey(
                name: "FK_Transitos_Vehiculos_VehiculoId",
                schema: "tracker",
                table: "Transitos");

            migrationBuilder.DropForeignKey(
                name: "FK_Transitos_Viajes_ViajeId",
                schema: "tracker",
                table: "Transitos");

            migrationBuilder.DropTable(
                name: "AsignacionesDispositivo",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "Viajes",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "Vehiculos",
                schema: "tracker");

            migrationBuilder.DropIndex(
                name: "ix_transito_vehiculo_autopista_fecha",
                schema: "tracker",
                table: "Transitos");

            migrationBuilder.DropIndex(
                name: "ix_transito_vehiculo_fechalocal",
                schema: "tracker",
                table: "Transitos");

            migrationBuilder.DropIndex(
                name: "ix_transito_viaje",
                schema: "tracker",
                table: "Transitos");

            migrationBuilder.DropIndex(
                name: "IX_Transitos_TarifaPorticoId",
                schema: "tracker",
                table: "Transitos");

            migrationBuilder.DropIndex(
                name: "ux_transito_device_portico_utc",
                schema: "tracker",
                table: "Transitos");

            migrationBuilder.DropIndex(
                name: "ix_gpsfix_viaje_utc",
                schema: "tracker",
                table: "gps_fix");

            migrationBuilder.DropColumn(
                name: "AutopistaSnapshot",
                schema: "tracker",
                table: "Transitos");

            migrationBuilder.DropColumn(
                name: "DiaTipo",
                schema: "tracker",
                table: "Transitos");

            migrationBuilder.DropColumn(
                name: "EstadoConciliacion",
                schema: "tracker",
                table: "Transitos");

            migrationBuilder.DropColumn(
                name: "FechaLocal",
                schema: "tracker",
                table: "Transitos");

            migrationBuilder.DropColumn(
                name: "HoraLocal",
                schema: "tracker",
                table: "Transitos");

            migrationBuilder.DropColumn(
                name: "PorticoCodigoSnapshot",
                schema: "tracker",
                table: "Transitos");

            migrationBuilder.DropColumn(
                name: "SentidoSnapshot",
                schema: "tracker",
                table: "Transitos");

            migrationBuilder.DropColumn(
                name: "TarifaPorticoId",
                schema: "tracker",
                table: "Transitos");

            migrationBuilder.DropColumn(
                name: "VehiculoId",
                schema: "tracker",
                table: "Transitos");

            migrationBuilder.DropColumn(
                name: "ViajeId",
                schema: "tracker",
                table: "Transitos");

            migrationBuilder.DropColumn(
                name: "viaje_id",
                schema: "tracker",
                table: "gps_fix");

            migrationBuilder.CreateTable(
                name: "TransitosPortico",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PorticoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DistanciaMetros = table.Column<double>(type: "float", nullable: false),
                    GpsPunto = table.Column<Point>(type: "geography", nullable: false),
                    HeadingGrados = table.Column<double>(type: "float", nullable: true),
                    RawId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    rowversion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Sentido = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceDeviceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VelocidadKmh = table.Column<double>(type: "float", nullable: true),
                    Via = table.Column<string>(type: "nvarchar(max)", nullable: true)
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
    }
}
