/* ===================================================================
   PRUEBA AISLADA del SQL de la migración.
   Crea una BD desechable, la usa y la borra. NO toca TrackerDb.
   =================================================================== */
SET NOCOUNT ON;

IF DB_ID('TrackerDbPrueba') IS NOT NULL
BEGIN
    ALTER DATABASE TrackerDbPrueba SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE TrackerDbPrueba;
END;
GO
CREATE DATABASE TrackerDbPrueba;
GO
USE TrackerDbPrueba;
GO
CREATE SCHEMA tracker;
GO

/* Réplica de tracker.Transitos: incluye rowversion y geography,
   que son las dos cosas que hicieron fallar el intento anterior. */
CREATE TABLE tracker.Transitos (
    Id                    uniqueidentifier NOT NULL PRIMARY KEY,
    PorticoId             uniqueidentifier NOT NULL,
    DeviceId              nvarchar(128)    NULL,
    VehiculoId            uniqueidentifier NULL,
    ViajeId               uniqueidentifier NULL,
    Utc                   datetime2        NOT NULL,
    FechaLocal            date             NOT NULL DEFAULT '0001-01-01',
    HoraLocal             time             NOT NULL DEFAULT '00:00:00',
    DiaTipo               int              NOT NULL DEFAULT 0,
    Banda                 int              NOT NULL DEFAULT 0,
    Categoria             int              NOT NULL DEFAULT 1,
    PrecioCalculado       decimal(18,4)    NOT NULL DEFAULT 0,
    TarifaPorticoId       uniqueidentifier NULL,
    AutopistaSnapshot     nvarchar(120)    NULL,
    PorticoCodigoSnapshot nvarchar(40)     NULL,
    SentidoSnapshot       nvarchar(80)     NULL,
    EstadoConciliacion    int              NOT NULL DEFAULT 0,
    Posicion              geography        NULL,
    ExactitudM            float            NULL,
    Fuente                nvarchar(32)     NOT NULL DEFAULT 'GPS',
    rowversion            rowversion       NOT NULL
);

CREATE TABLE tracker.Porticos (
    Id        uniqueidentifier NOT NULL PRIMARY KEY,
    Codigo    nvarchar(40)  NOT NULL,
    Autopista nvarchar(120) NOT NULL,
    Sentido   nvarchar(80)  NOT NULL
);
GO

DECLARE @p uniqueidentifier = NEWID();
INSERT INTO tracker.Porticos (Id, Codigo, Autopista, Sentido)
VALUES (@p, 'P5', 'Autopista Central', 'Norte - Sur');

/* Caso clave: 23:30 hora de Chile = 02:30 UTC del día SIGUIENTE.
   Si el backfill funciona, FechaLocal debe quedar en el día 15, no el 16. */
DECLARE @utcNoche datetime2 = '2026-03-16T02:30:00';
DECLARE @utcDia   datetime2 = '2026-03-16T14:00:00';
DECLARE @dup      datetime2 = '2026-03-17T13:00:00';

INSERT INTO tracker.Transitos (Id, PorticoId, DeviceId, Utc, PrecioCalculado, Posicion)
VALUES
  (NEWID(), @p, 'dev-1', @utcNoche, 1000, geography::Point(-33.45, -70.66, 4326)),
  (NEWID(), @p, 'dev-1', @utcDia,    900, geography::Point(-33.45, -70.66, 4326)),
  -- Dos duplicados exactos del mismo paso (mismo device, pórtico e instante)
  (NEWID(), @p, 'dev-1', @dup,       800, NULL),
  (NEWID(), @p, 'dev-1', @dup,       800, NULL),
  (NEWID(), @p, 'dev-1', @dup,       800, NULL);

PRINT '--- Filas iniciales ---';
SELECT COUNT(*) AS TotalInicial FROM tracker.Transitos;
GO

/* ============ 1) Fecha y hora locales ============ */
UPDATE t
SET FechaLocal = CAST(l.LocalDt AS date),
    HoraLocal  = CAST(l.LocalDt AS time(0))
FROM tracker.Transitos AS t
CROSS APPLY (
    SELECT CAST(t.Utc AT TIME ZONE 'UTC'
                      AT TIME ZONE 'Pacific SA Standard Time' AS datetime2(0)) AS LocalDt
) AS l;
PRINT '1) AT TIME ZONE: OK';
GO

/* ============ 2) Tipo de día ============ */
UPDATE tracker.Transitos
SET DiaTipo = CASE DATEDIFF(day, '19050101', FechaLocal) % 7
                WHEN 0 THEN 2 WHEN 6 THEN 1 ELSE 0 END
WHERE FechaLocal > '0001-01-01';
PRINT '2) DiaTipo: OK';
GO

/* ============ 3) Snapshots ============ */
UPDATE t
SET AutopistaSnapshot     = p.Autopista,
    PorticoCodigoSnapshot = p.Codigo,
    SentidoSnapshot       = p.Sentido
FROM tracker.Transitos AS t
INNER JOIN tracker.Porticos AS p ON p.Id = t.PorticoId;
PRINT '3) Snapshots: OK';
GO

/* ============ 4) Tabla de respaldo ============ */
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
PRINT '4) Tabla de respaldo: OK';
GO

/* ============ 5) Archivar y borrar duplicados ============ */
WITH duplicados AS (
    SELECT Id,
           ROW_NUMBER() OVER (PARTITION BY DeviceId, PorticoId, Utc ORDER BY Id) AS rn
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
PRINT '5) Archivado + borrado: OK';
GO

/* ============ 6) Índice único ============ */
CREATE UNIQUE INDEX ux_transito_device_portico_utc
    ON tracker.Transitos (DeviceId, PorticoId, Utc)
    WHERE DeviceId IS NOT NULL;
PRINT '6) Indice unico: OK';
GO

/* ============ RESULTADOS ============ */
PRINT '';
PRINT '================ RESULTADOS ================';
SELECT 'Transitos que quedan' AS Chequeo, COUNT(*) AS Valor FROM tracker.Transitos
UNION ALL SELECT 'Duplicados archivados', COUNT(*) FROM tracker.TransitosDuplicadosRespaldo;

PRINT '';
PRINT 'El de las 02:30 UTC debe mostrar fecha 2026-03-15 y hora 23:30 (no el 16):';
SELECT CONVERT(varchar(19), Utc, 126) AS Utc,
       CONVERT(varchar(10), FechaLocal, 23) AS FechaLocal,
       CONVERT(varchar(8), HoraLocal, 108)  AS HoraLocal,
       DiaTipo, AutopistaSnapshot, PorticoCodigoSnapshot
FROM tracker.Transitos ORDER BY Utc;
GO

USE master;
GO
ALTER DATABASE TrackerDbPrueba SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE TrackerDbPrueba;
PRINT '';
PRINT 'BD de prueba eliminada.';
GO
