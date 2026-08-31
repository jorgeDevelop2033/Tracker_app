-- ============================================================
-- TRACKER - Usuario administrador inicial + asignación de roles
-- Base de datos: AuthDb
-- Orden de ejecución: 4 de 4  (requiere 01, 02 y 03 ejecutados)
-- Idempotente: seguro de ejecutar múltiples veces
--
-- ⚠️ IMPORTANTE — HASH DE CONTRASEÑA
-- El hash NO se puede generar en T-SQL. El servicio Auth usa
-- PBKDF2-HMAC-SHA256 con 210.000 iteraciones, salt de 16 bytes y un PEPPER
-- secreto que vive fuera del repositorio (variable de entorno).
-- El formato almacenado es:  pbkdf2-sha256:{iter}${saltB64}${hashB64}
--
-- Genere el hash ANTES de ejecutar este script con el helper:
--     dotnet run --project tools/HashPassword -- "MiClaveSegura"
-- (o vea db/auth/README.md). Pegue el resultado en @AdminPasswordHash.
--
-- Si deja @AdminPasswordHash en NULL, el usuario se crea con estado
-- Inactivo (4) y sin hash utilizable: deberá completar el alta por el
-- flujo de recuperación de contraseña del servicio Auth.
-- ============================================================

SET NOCOUNT ON;
GO

DECLARE @now datetime2(7) = SYSUTCDATETIME();

-- ============================================================
-- PARÁMETROS — AJUSTAR POR AMBIENTE
-- ============================================================
DECLARE @B2BCodigo nvarchar(60) = N'TRACKER-DEMO';   -- mismo valor del script 02
DECLARE @GlobalTenantId uniqueidentifier = '11111111-1111-1111-1111-111111111111';

-- Admin de back-office (vive en el tenant GLOBAL)
DECLARE @AdminNombre nvarchar(120) = N'Administrador Tracker';
DECLARE @AdminEmail  nvarchar(160) = N'admin@tracker.devsogu.cl';

-- Admin de la empresa de flota (vive en el tenant B2B)
DECLARE @FlotaNombre nvarchar(120) = N'Administrador Flota';
DECLARE @FlotaEmail  nvarchar(160) = N'flota@tracker.devsogu.cl';

-- 👇 PEGAR AQUÍ EL HASH GENERADO. NULL = usuario inactivo sin clave.
DECLARE @AdminPasswordHash nvarchar(400) = NULL;
DECLARE @FlotaPasswordHash nvarchar(400) = NULL;

-- Enums (ver Auth.Domain): EstadoUsuario Activo=1 Inactivo=4
--                          TipoUsuario   Interno=1 Cliente=3
DECLARE @EstadoActivo   int = 1;
DECLARE @EstadoInactivo int = 4;
DECLARE @TipoInterno    int = 1;

-- Normalizar emails: el servicio los guarda en minúsculas
SET @AdminEmail = LOWER(LTRIM(RTRIM(@AdminEmail)));
SET @FlotaEmail = LOWER(LTRIM(RTRIM(@FlotaEmail)));

DECLARE @B2BTenantId uniqueidentifier;
SELECT @B2BTenantId = Id FROM Tenants WHERE Codigo = @B2BCodigo;

IF @B2BTenantId IS NULL
BEGIN
    RAISERROR('No existe el tenant B2B %s. Ejecute 02_tracker_tenant.sql', 16, 1, @B2BCodigo);
    RETURN;
END

BEGIN TRY
    BEGIN TRANSACTION;

    -- ========================================================
    -- 1) ADMIN BACK-OFFICE (tenant GLOBAL)
    -- ========================================================
    DECLARE @AdminId       uniqueidentifier;
    DECLARE @SucGlobalId   uniqueidentifier;

    SELECT TOP 1 @SucGlobalId = Id FROM Sucursales
    WHERE TenantId = @GlobalTenantId AND Codigo = N'GLOBAL';

    SELECT @AdminId = Id FROM Usuarios
    WHERE TenantId = @GlobalTenantId AND Email = @AdminEmail;

    IF @AdminId IS NULL
    BEGIN
        SET @AdminId = NEWID();

        INSERT INTO Usuarios (Id, TenantId, SucursalId, Tipo, Nombre, Email, PasswordHash,
                              Estado, EmailVerificado, IntentosFallidos, FechaCreacion, FechaActualizacion)
        VALUES (@AdminId, @GlobalTenantId, @SucGlobalId, @TipoInterno, @AdminNombre, @AdminEmail,
                ISNULL(@AdminPasswordHash, N'!'),
                CASE WHEN @AdminPasswordHash IS NULL THEN @EstadoInactivo ELSE @EstadoActivo END,
                1, 0, @now, @now);

        PRINT 'Usuario admin creado: ' + @AdminEmail;
    END
    ELSE
    BEGIN
        -- Solo actualizar la clave si se entregó una nueva
        IF @AdminPasswordHash IS NOT NULL
        BEGIN
            UPDATE Usuarios
            SET    PasswordHash = @AdminPasswordHash,
                   Estado = @EstadoActivo,
                   FechaActualizacion = @now
            WHERE  Id = @AdminId;

            PRINT 'Usuario admin ya existia: clave actualizada.';
        END
        ELSE
            PRINT 'Usuario admin ya existia: sin cambios (no se entrego hash).';
    END

    -- Roles del admin: TrackerAdmin en el tenant global
    INSERT INTO UsuarioRoles (TenantId, UsuarioId, RolId)
    SELECT @GlobalTenantId, @AdminId, r.Id
    FROM   Roles r
    WHERE  r.TenantId = @GlobalTenantId
      AND  r.Nombre IN (N'TrackerAdmin')
      AND  r.Eliminado = 0
      AND  NOT EXISTS (SELECT 1 FROM UsuarioRoles ur
                       WHERE ur.TenantId = @GlobalTenantId
                         AND ur.UsuarioId = @AdminId
                         AND ur.RolId = r.Id);

    -- ========================================================
    -- 2) ADMIN DE FLOTA (tenant B2B)
    -- ========================================================
    DECLARE @FlotaId     uniqueidentifier;
    DECLARE @SucB2BId    uniqueidentifier;

    SELECT TOP 1 @SucB2BId = Id FROM Sucursales
    WHERE TenantId = @B2BTenantId AND Codigo = N'MATRIZ';

    SELECT @FlotaId = Id FROM Usuarios
    WHERE TenantId = @B2BTenantId AND Email = @FlotaEmail;

    IF @FlotaId IS NULL
    BEGIN
        SET @FlotaId = NEWID();

        INSERT INTO Usuarios (Id, TenantId, SucursalId, Tipo, Nombre, Email, PasswordHash,
                              Estado, EmailVerificado, IntentosFallidos, FechaCreacion, FechaActualizacion)
        VALUES (@FlotaId, @B2BTenantId, @SucB2BId, @TipoInterno, @FlotaNombre, @FlotaEmail,
                ISNULL(@FlotaPasswordHash, N'!'),
                CASE WHEN @FlotaPasswordHash IS NULL THEN @EstadoInactivo ELSE @EstadoActivo END,
                1, 0, @now, @now);

        PRINT 'Usuario admin de flota creado: ' + @FlotaEmail;
    END
    ELSE
    BEGIN
        IF @FlotaPasswordHash IS NOT NULL
        BEGIN
            UPDATE Usuarios
            SET    PasswordHash = @FlotaPasswordHash,
                   Estado = @EstadoActivo,
                   FechaActualizacion = @now
            WHERE  Id = @FlotaId;

            PRINT 'Usuario de flota ya existia: clave actualizada.';
        END
        ELSE
            PRINT 'Usuario de flota ya existia: sin cambios.';
    END

    -- Roles del admin de flota
    INSERT INTO UsuarioRoles (TenantId, UsuarioId, RolId)
    SELECT @B2BTenantId, @FlotaId, r.Id
    FROM   Roles r
    WHERE  r.TenantId = @B2BTenantId
      AND  r.Nombre IN (N'TrackerFlotaAdmin')
      AND  r.Eliminado = 0
      AND  NOT EXISTS (SELECT 1 FROM UsuarioRoles ur
                       WHERE ur.TenantId = @B2BTenantId
                         AND ur.UsuarioId = @FlotaId
                         AND ur.RolId = r.Id);

    COMMIT TRANSACTION;
    PRINT 'OK: usuarios y roles provisionados.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

-- ============================================================
-- VERIFICACIÓN
-- ============================================================
SELECT t.Codigo AS Tenant,
       u.Email,
       u.Nombre,
       u.Estado,
       CASE WHEN u.PasswordHash LIKE 'pbkdf2-sha256:%' THEN 'SI' ELSE 'NO (pendiente)' END AS ClaveUsable,
       STUFF((SELECT ', ' + r2.Nombre
              FROM UsuarioRoles ur2
              JOIN Roles r2 ON r2.TenantId = ur2.TenantId AND r2.Id = ur2.RolId
              WHERE ur2.TenantId = u.TenantId AND ur2.UsuarioId = u.Id
              FOR XML PATH('')), 1, 2, '') AS Roles
FROM   Usuarios u
JOIN   Tenants  t ON t.Id = u.TenantId
WHERE  u.Email IN (@AdminEmail, @FlotaEmail)
ORDER  BY t.Codigo;
GO
