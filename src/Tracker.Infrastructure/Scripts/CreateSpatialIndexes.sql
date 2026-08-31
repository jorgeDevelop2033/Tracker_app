-- ============================================================
-- Índices espaciales de TrackerDb
--
-- YA NO ES UN PASO MANUAL: los crea la migración
-- 20260830230000_IndicesEspaciales, que corre sola al arrancar
-- Tracker.API (MigrateAsync). Este script queda solo para aplicarlos
-- a mano sobre una base existente sin desplegar.
--
-- Sin estos índices, el GetNearAsync del detector hace scan completo de
-- tracker.Porticos calculando STDistance fila por fila en cada fix GPS.
--
-- OJO: el schema es 'tracker', no 'dbo'. Este script apuntaba a dbo y por
-- eso nunca surtió efecto en producción.
-- ============================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Porticos_Ubicacion'
                 AND object_id = OBJECT_ID('tracker.Porticos'))
    CREATE SPATIAL INDEX IX_Porticos_Ubicacion
    ON tracker.Porticos(Ubicacion)
    USING GEOGRAPHY_GRID
    WITH (GRIDS = (MEDIUM, MEDIUM, MEDIUM, MEDIUM), CELLS_PER_OBJECT = 16);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Porticos_Corredor'
                 AND object_id = OBJECT_ID('tracker.Porticos'))
    CREATE SPATIAL INDEX IX_Porticos_Corredor
    ON tracker.Porticos(Corredor)
    USING GEOGRAPHY_GRID
    WITH (GRIDS = (MEDIUM, MEDIUM, MEDIUM, MEDIUM), CELLS_PER_OBJECT = 16);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Transitos_Posicion'
                 AND object_id = OBJECT_ID('tracker.Transitos'))
    CREATE SPATIAL INDEX IX_Transitos_Posicion
    ON tracker.Transitos(Posicion)
    USING GEOGRAPHY_GRID
    WITH (GRIDS = (MEDIUM, MEDIUM, MEDIUM, MEDIUM), CELLS_PER_OBJECT = 16);
GO

SELECT OBJECT_NAME(object_id) AS Tabla, name AS Indice
FROM   sys.indexes
WHERE  type = 4
ORDER  BY Tabla, Indice;
GO
