-- ============================================================
-- TRACKER - Roles por plan + mapeo de permisos
-- Base de datos: AuthDb
-- Orden de ejecución: 3 de 4  (requiere 01 y 02 ejecutados)
-- Idempotente: seguro de ejecutar múltiples veces
--
-- CÓMO SE MODELA EL PLAN
-- ----------------------
-- No existe tabla de Planes ni motor de features. El plan contratado se
-- materializa como UN ROL con su set de permisos. Al usuario que contrata
-- "Pro" se le asigna el rol TrackerPro y con eso su JWT lleva los permisos
-- de ese plan. Subir/bajar de plan = cambiar el rol asignado.
--
-- MATRIZ DE PLANES
-- ----------------------------------------------------------------
--                             Free    Pro     Flota(B2B)
--   Ver pórticos en mapa       si      si       si
--   Emitir GPS / tracking      si      si       si
--   Alerta de paso + precio    si      si       si
--   Gasto resumen              si      si       si
--   Gasto detalle              no      si       si
--   Historial de recorridos    no      si       si
--   Exportar gastos/reportes   no      si       si
--   Multi-vehículo (CRUD)      no      si       si
--   Seguir en vivo             no      si       si
--   Ver flota completa         no      no       si
--   Asignar conductores        no      no       si
-- ----------------------------------------------------------------
--
-- Roles NO ligados a plan (operación interna):
--   TrackerAdmin    -> back-office: carga de tarifas, bandas, pórticos
--   TrackerSoporte  -> solo lectura transversal para mesa de ayuda
-- ============================================================

SET NOCOUNT ON;
GO

DECLARE @now datetime2(7) = SYSUTCDATETIME();

-- ============================================================
-- PARÁMETROS — AJUSTAR POR AMBIENTE
-- ============================================================
DECLARE @B2BCodigo nvarchar(60) = N'TRACKER-DEMO';  -- mismo valor del script 02
DECLARE @GlobalTenantId uniqueidentifier = '11111111-1111-1111-1111-111111111111';

DECLARE @B2BTenantId uniqueidentifier;
SELECT @B2BTenantId = Id FROM Tenants WHERE Codigo = @B2BCodigo;

IF @B2BTenantId IS NULL
BEGIN
    RAISERROR('No existe el tenant B2B con codigo %s. Ejecute primero 02_tracker_tenant.sql', 16, 1, @B2BCodigo);
    RETURN;
END

IF NOT EXISTS (SELECT 1 FROM Tenants WHERE Id = @GlobalTenantId)
BEGIN
    RAISERROR('No existe el tenant GLOBAL. Ejecute primero 02_tracker_tenant.sql', 16, 1);
    RETURN;
END

-- ============================================================
-- Tabla temporal: definición (tenant, rol, descripcion, esSistema)
-- ============================================================
DECLARE @RolesDef TABLE (
    TenantId    uniqueidentifier,
    Nombre      nvarchar(80),
    Descripcion nvarchar(200),
    EsSistema   bit
);

-- Roles del tenant GLOBAL (B2C): planes de conductor individual
INSERT INTO @RolesDef (TenantId, Nombre, Descripcion, EsSistema) VALUES
    (@GlobalTenantId, N'TrackerFree',    N'Plan Free: mapa, tracking y gasto resumido',              1),
    (@GlobalTenantId, N'TrackerPro',     N'Plan Pro: multi-vehiculo, historial, detalle y export',   1),
    (@GlobalTenantId, N'TrackerAdmin',   N'Back-office Tracker: tarifas, bandas y porticos',         1),
    (@GlobalTenantId, N'TrackerSoporte', N'Mesa de ayuda: solo lectura transversal',                 1);

-- Roles del tenant B2B (empresa de flota)
INSERT INTO @RolesDef (TenantId, Nombre, Descripcion, EsSistema) VALUES
    (@B2BTenantId, N'TrackerFlotaAdmin',    N'Plan Flota: administra vehiculos, conductores y gasto', 1),
    (@B2BTenantId, N'TrackerFlotaConductor',N'Conductor de flota: emite GPS y ve su propio gasto',    1),
    (@B2BTenantId, N'TrackerFlotaSupervisor',N'Supervisor: ve toda la flota en vivo, sin editar',     1),
    (@B2BTenantId, N'TrackerAdmin',         N'Back-office Tracker: tarifas, bandas y porticos',       1);

-- ============================================================
-- 1) UPSERT de roles
-- ============================================================
INSERT INTO Roles (Id, TenantId, Nombre, Descripcion, EsSistema, Eliminado, FechaCreacion, FechaActualizacion)
SELECT NEWID(), d.TenantId, d.Nombre, d.Descripcion, d.EsSistema, 0, @now, @now
FROM   @RolesDef d
WHERE  NOT EXISTS (
        SELECT 1 FROM Roles r
        WHERE r.TenantId = d.TenantId AND r.Nombre = d.Nombre);

-- Reactivar y sincronizar descripción si el rol ya existía
UPDATE r
SET    r.Descripcion = d.Descripcion,
       r.EsSistema   = d.EsSistema,
       r.Eliminado   = 0,
       r.FechaActualizacion = @now
FROM   Roles r
JOIN   @RolesDef d ON d.TenantId = r.TenantId AND d.Nombre = r.Nombre
WHERE  r.Descripcion <> d.Descripcion
    OR r.EsSistema   <> d.EsSistema
    OR r.Eliminado   <> 0;

-- ============================================================
-- 2) Matriz rol -> permisos
--    Se declara por (TenantId, RolNombre, CodigoPermiso).
-- ============================================================
DECLARE @Matriz TABLE (
    TenantId  uniqueidentifier,
    RolNombre nvarchar(80),
    Codigo    nvarchar(160)
);

-- ---------- Base común a TODO plan de conductor ----------
DECLARE @Base TABLE (Codigo nvarchar(160));
INSERT INTO @Base (Codigo) VALUES
    ('tracker.porticos.ver'),
    ('tracker.porticos.listar'),
    ('tracker.tracking.emitir'),
    ('tracker.tracking.ver'),
    ('tracker.alertas.recibir'),
    ('tracker.gastos.resumen'),
    ('tracker.transitos.ver'),
    ('tracker.vehiculos.ver'),
    ('tracker.vehiculos.listar');

-- ---------- FREE = solo la base ----------
INSERT INTO @Matriz (TenantId, RolNombre, Codigo)
SELECT @GlobalTenantId, N'TrackerFree', Codigo FROM @Base;

-- ---------- PRO = base + historial, detalle, export, multi-vehículo, vivo ----------
INSERT INTO @Matriz (TenantId, RolNombre, Codigo)
SELECT @GlobalTenantId, N'TrackerPro', Codigo FROM @Base;

INSERT INTO @Matriz (TenantId, RolNombre, Codigo) VALUES
    (@GlobalTenantId, N'TrackerPro', 'tracker.gastos.detalle'),
    (@GlobalTenantId, N'TrackerPro', 'tracker.gastos.exportar'),
    (@GlobalTenantId, N'TrackerPro', 'tracker.tracking.historial'),
    (@GlobalTenantId, N'TrackerPro', 'tracker.tracking.vivo'),
    (@GlobalTenantId, N'TrackerPro', 'tracker.vehiculos.crear'),
    (@GlobalTenantId, N'TrackerPro', 'tracker.vehiculos.actualizar'),
    (@GlobalTenantId, N'TrackerPro', 'tracker.vehiculos.eliminar'),
    (@GlobalTenantId, N'TrackerPro', 'tracker.alertas.configurar'),
    (@GlobalTenantId, N'TrackerPro', 'tracker.reportes.ver'),
    (@GlobalTenantId, N'TrackerPro', 'tracker.reportes.exportar'),
    (@GlobalTenantId, N'TrackerPro', 'tracker.tarifas.ver'),
    (@GlobalTenantId, N'TrackerPro', 'tracker.bandas.ver');

-- ---------- SOPORTE (global) = solo lectura transversal ----------
INSERT INTO @Matriz (TenantId, RolNombre, Codigo) VALUES
    (@GlobalTenantId, N'TrackerSoporte', 'tracker.porticos.ver'),
    (@GlobalTenantId, N'TrackerSoporte', 'tracker.porticos.listar'),
    (@GlobalTenantId, N'TrackerSoporte', 'tracker.tarifas.ver'),
    (@GlobalTenantId, N'TrackerSoporte', 'tracker.bandas.ver'),
    (@GlobalTenantId, N'TrackerSoporte', 'tracker.transitos.ver'),
    (@GlobalTenantId, N'TrackerSoporte', 'tracker.gastos.resumen'),
    (@GlobalTenantId, N'TrackerSoporte', 'tracker.gastos.detalle'),
    (@GlobalTenantId, N'TrackerSoporte', 'tracker.tracking.ver'),
    (@GlobalTenantId, N'TrackerSoporte', 'tracker.tracking.historial'),
    (@GlobalTenantId, N'TrackerSoporte', 'tracker.vehiculos.ver'),
    (@GlobalTenantId, N'TrackerSoporte', 'tracker.vehiculos.listar'),
    (@GlobalTenantId, N'TrackerSoporte', 'tracker.reportes.ver');

-- ---------- ADMIN back-office = TODOS los permisos tracker.* ----------
-- Se define en ambos tenants (global y B2B).
INSERT INTO @Matriz (TenantId, RolNombre, Codigo)
SELECT @GlobalTenantId, N'TrackerAdmin', p.Codigo
FROM   Permisos p WHERE p.Codigo LIKE 'tracker.%' AND p.Activo = 1;

INSERT INTO @Matriz (TenantId, RolNombre, Codigo)
SELECT @B2BTenantId, N'TrackerAdmin', p.Codigo
FROM   Permisos p WHERE p.Codigo LIKE 'tracker.%' AND p.Activo = 1;

-- ---------- FLOTA CONDUCTOR = base (su propio vehículo) ----------
INSERT INTO @Matriz (TenantId, RolNombre, Codigo)
SELECT @B2BTenantId, N'TrackerFlotaConductor', Codigo FROM @Base;

INSERT INTO @Matriz (TenantId, RolNombre, Codigo) VALUES
    (@B2BTenantId, N'TrackerFlotaConductor', 'tracker.tracking.historial'),
    (@B2BTenantId, N'TrackerFlotaConductor', 'tracker.gastos.detalle');

-- ---------- FLOTA SUPERVISOR = ve toda la flota, sin editar ----------
INSERT INTO @Matriz (TenantId, RolNombre, Codigo)
SELECT @B2BTenantId, N'TrackerFlotaSupervisor', Codigo FROM @Base;

INSERT INTO @Matriz (TenantId, RolNombre, Codigo) VALUES
    (@B2BTenantId, N'TrackerFlotaSupervisor', 'tracker.tracking.vivo'),
    (@B2BTenantId, N'TrackerFlotaSupervisor', 'tracker.tracking.historial'),
    (@B2BTenantId, N'TrackerFlotaSupervisor', 'tracker.tracking.flota'),
    (@B2BTenantId, N'TrackerFlotaSupervisor', 'tracker.gastos.detalle'),
    (@B2BTenantId, N'TrackerFlotaSupervisor', 'tracker.gastos.flota'),
    (@B2BTenantId, N'TrackerFlotaSupervisor', 'tracker.gastos.exportar'),
    (@B2BTenantId, N'TrackerFlotaSupervisor', 'tracker.reportes.ver'),
    (@B2BTenantId, N'TrackerFlotaSupervisor', 'tracker.reportes.exportar');

-- ---------- FLOTA ADMIN = supervisor + CRUD vehículos y conductores ----------
INSERT INTO @Matriz (TenantId, RolNombre, Codigo)
SELECT @B2BTenantId, N'TrackerFlotaAdmin', Codigo FROM @Base;

INSERT INTO @Matriz (TenantId, RolNombre, Codigo) VALUES
    (@B2BTenantId, N'TrackerFlotaAdmin', 'tracker.tracking.vivo'),
    (@B2BTenantId, N'TrackerFlotaAdmin', 'tracker.tracking.historial'),
    (@B2BTenantId, N'TrackerFlotaAdmin', 'tracker.tracking.flota'),
    (@B2BTenantId, N'TrackerFlotaAdmin', 'tracker.gastos.detalle'),
    (@B2BTenantId, N'TrackerFlotaAdmin', 'tracker.gastos.flota'),
    (@B2BTenantId, N'TrackerFlotaAdmin', 'tracker.gastos.exportar'),
    (@B2BTenantId, N'TrackerFlotaAdmin', 'tracker.reportes.ver'),
    (@B2BTenantId, N'TrackerFlotaAdmin', 'tracker.reportes.exportar'),
    (@B2BTenantId, N'TrackerFlotaAdmin', 'tracker.vehiculos.crear'),
    (@B2BTenantId, N'TrackerFlotaAdmin', 'tracker.vehiculos.actualizar'),
    (@B2BTenantId, N'TrackerFlotaAdmin', 'tracker.vehiculos.eliminar'),
    (@B2BTenantId, N'TrackerFlotaAdmin', 'tracker.vehiculos.asignar_conductor'),
    (@B2BTenantId, N'TrackerFlotaAdmin', 'tracker.alertas.configurar'),
    (@B2BTenantId, N'TrackerFlotaAdmin', 'tracker.tarifas.ver'),
    (@B2BTenantId, N'TrackerFlotaAdmin', 'tracker.bandas.ver');

-- ============================================================
-- 3) Resolver la matriz a IDs reales
-- ============================================================
DECLARE @Deseado TABLE (
    TenantId  uniqueidentifier,
    RolId     uniqueidentifier,
    PermisoId uniqueidentifier,
    PRIMARY KEY (TenantId, RolId, PermisoId)
);

INSERT INTO @Deseado (TenantId, RolId, PermisoId)
SELECT DISTINCT r.TenantId, r.Id, p.Id
FROM   @Matriz m
JOIN   Roles    r ON r.TenantId = m.TenantId AND r.Nombre = m.RolNombre
JOIN   Permisos p ON p.Codigo   = m.Codigo   AND p.Activo = 1;

-- Aviso si algún código de la matriz no existe en el catálogo
IF EXISTS (SELECT 1 FROM @Matriz m
           WHERE NOT EXISTS (SELECT 1 FROM Permisos p WHERE p.Codigo = m.Codigo))
BEGIN
    PRINT '*** ADVERTENCIA: hay codigos en la matriz sin permiso en el catalogo:';
    SELECT DISTINCT m.Codigo AS CodigoFaltante
    FROM   @Matriz m
    WHERE  NOT EXISTS (SELECT 1 FROM Permisos p WHERE p.Codigo = m.Codigo);
END

-- ============================================================
-- 4) Sincronizar RolPermisos (agregar faltantes, quitar sobrantes)
--    Solo toca los roles definidos en este script.
-- ============================================================
DECLARE @RolesGestionados TABLE (TenantId uniqueidentifier, RolId uniqueidentifier);

INSERT INTO @RolesGestionados (TenantId, RolId)
SELECT r.TenantId, r.Id
FROM   Roles r
JOIN   @RolesDef d ON d.TenantId = r.TenantId AND d.Nombre = r.Nombre;

-- 4.a Agregar los que faltan
INSERT INTO RolPermisos (TenantId, RolId, PermisoId)
SELECT d.TenantId, d.RolId, d.PermisoId
FROM   @Deseado d
WHERE  NOT EXISTS (
        SELECT 1 FROM RolPermisos rp
        WHERE rp.TenantId = d.TenantId AND rp.RolId = d.RolId AND rp.PermisoId = d.PermisoId);

-- 4.b Quitar los sobrantes SOLO de permisos tracker.* en roles gestionados.
--     Se limita a tracker.* para no borrar permisos de otros módulos que
--     alguien haya agregado manualmente a estos roles.
DELETE rp
FROM   RolPermisos rp
JOIN   @RolesGestionados g ON g.TenantId = rp.TenantId AND g.RolId = rp.RolId
JOIN   Permisos p          ON p.Id = rp.PermisoId
WHERE  p.Codigo LIKE 'tracker.%'
  AND  NOT EXISTS (
        SELECT 1 FROM @Deseado d
        WHERE d.TenantId = rp.TenantId AND d.RolId = rp.RolId AND d.PermisoId = rp.PermisoId);

-- ============================================================
-- VERIFICACIÓN
-- ============================================================
SELECT t.Codigo AS Tenant,
       r.Nombre AS Rol,
       COUNT(rp.PermisoId) AS Permisos
FROM   Roles r
JOIN   Tenants t ON t.Id = r.TenantId
LEFT   JOIN RolPermisos rp ON rp.TenantId = r.TenantId AND rp.RolId = r.Id
WHERE  r.Nombre LIKE 'Tracker%' AND r.Eliminado = 0
GROUP  BY t.Codigo, r.Nombre
ORDER  BY t.Codigo, r.Nombre;

-- Detalle por rol (descomentar si se quiere auditar)
-- SELECT t.Codigo AS Tenant, r.Nombre AS Rol, p.Codigo AS Permiso
-- FROM   RolPermisos rp
-- JOIN   Roles r    ON r.TenantId = rp.TenantId AND r.Id = rp.RolId
-- JOIN   Tenants t  ON t.Id = r.TenantId
-- JOIN   Permisos p ON p.Id = rp.PermisoId
-- WHERE  r.Nombre LIKE 'Tracker%'
-- ORDER  BY t.Codigo, r.Nombre, p.Codigo;
GO
