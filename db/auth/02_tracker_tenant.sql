-- ============================================================
-- TRACKER - Creación de Tenants
-- Base de datos: AuthDb
-- Orden de ejecución: 2 de 4
-- Idempotente: seguro de ejecutar múltiples veces
--
-- MODELO HÍBRIDO:
--   * Tenant GLOBAL (B2C)  -> conductores individuales que se registran
--                             solos en la app mobile. Id FIJO
--                             11111111-1111-1111-1111-111111111111 porque el
--                             código lo tiene hardcodeado en
--                             Auth.Domain/Tenancy/TenancyDefaults.cs y lo usa
--                             el query filter de AuthDbContext.
--   * Tenant B2B           -> una empresa de flota que contrata. Se crea uno
--                             por cliente, con su propio Codigo y Plan.
--
-- El campo Tenants.Plan es TEXTO LIBRE (nvarchar(60)). No hay tabla de planes
-- ni validación en código: es una etiqueta informativa/comercial. La
-- capacidad REAL la otorgan los roles del script 03.
-- ============================================================

SET NOCOUNT ON;
GO

DECLARE @now datetime2(7) = SYSUTCDATETIME();

-- ============================================================
-- PARÁMETROS DEL TENANT B2B — AJUSTAR POR AMBIENTE / CLIENTE
-- ============================================================
DECLARE @B2BNombre   nvarchar(120) = N'Tracker Demo Flota';
DECLARE @B2BCodigo   nvarchar(60)  = N'TRACKER-DEMO';   -- único global
DECLARE @B2BPlan     nvarchar(60)  = N'flota';          -- free | pro | flota
DECLARE @B2BSucursal nvarchar(120) = N'Casa Matriz';

DECLARE @GlobalTenantId uniqueidentifier = '11111111-1111-1111-1111-111111111111';

-- ============================================================
-- 1) TENANT GLOBAL (B2C) — Id fijo, NO cambiar
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Tenants WHERE Id = @GlobalTenantId)
BEGIN
    INSERT INTO Tenants (Id, Nombre, Codigo, [Plan], Activo, FechaCreacion, FechaActualizacion)
    VALUES (@GlobalTenantId, N'GLOBAL', N'GLOBAL', N'free', 1, @now, @now);

    PRINT 'Tenant GLOBAL (B2C) creado.';
END
ELSE
    PRINT 'Tenant GLOBAL (B2C) ya existía, sin cambios.';

-- Sucursal por defecto del tenant global.
-- Usuario.SucursalId es opcional, pero el bootstrapper del ERP siempre
-- provisiona una, así que la dejamos para mantener el mismo patrón.
IF NOT EXISTS (SELECT 1 FROM Sucursales WHERE TenantId = @GlobalTenantId AND Codigo = N'GLOBAL')
BEGIN
    INSERT INTO Sucursales (Id, TenantId, Nombre, Codigo, Direccion, Activa, ManejaStock, FechaCreacion, FechaActualizacion)
    VALUES (NEWID(), @GlobalTenantId, N'Global', N'GLOBAL', NULL, 1, 0, @now, @now);

    PRINT 'Sucursal GLOBAL creada.';
END

-- ============================================================
-- 2) TENANT B2B (empresa de flota)
-- ============================================================
DECLARE @B2BTenantId uniqueidentifier;

SELECT @B2BTenantId = Id FROM Tenants WHERE Codigo = @B2BCodigo;

IF @B2BTenantId IS NULL
BEGIN
    SET @B2BTenantId = NEWID();

    INSERT INTO Tenants (Id, Nombre, Codigo, [Plan], Activo, FechaCreacion, FechaActualizacion)
    VALUES (@B2BTenantId, @B2BNombre, @B2BCodigo, @B2BPlan, 1, @now, @now);

    PRINT 'Tenant B2B creado: ' + @B2BCodigo;
END
ELSE
BEGIN
    -- Mantener nombre y plan alineados con los parámetros de este script
    UPDATE Tenants
    SET    Nombre = @B2BNombre,
           [Plan] = @B2BPlan,
           FechaActualizacion = @now
    WHERE  Id = @B2BTenantId
      AND (Nombre <> @B2BNombre OR ISNULL([Plan],'') <> @B2BPlan);

    PRINT 'Tenant B2B ya existía: ' + @B2BCodigo + ' (plan sincronizado).';
END

-- Sucursal del tenant B2B
IF NOT EXISTS (SELECT 1 FROM Sucursales WHERE TenantId = @B2BTenantId AND Codigo = N'MATRIZ')
BEGIN
    INSERT INTO Sucursales (Id, TenantId, Nombre, Codigo, Direccion, Activa, ManejaStock, FechaCreacion, FechaActualizacion)
    VALUES (NEWID(), @B2BTenantId, @B2BSucursal, N'MATRIZ', NULL, 1, 0, @now, @now);

    PRINT 'Sucursal MATRIZ creada para ' + @B2BCodigo;
END

-- ============================================================
-- VERIFICACIÓN
-- ============================================================
SELECT t.Id, t.Nombre, t.Codigo, t.[Plan], t.Activo,
       (SELECT COUNT(*) FROM Sucursales s WHERE s.TenantId = t.Id) AS Sucursales
FROM   Tenants t
WHERE  t.Id = @GlobalTenantId OR t.Codigo = @B2BCodigo
ORDER  BY t.Codigo;
GO
