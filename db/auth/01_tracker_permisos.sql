-- ============================================================
-- TRACKER - Catálogo GLOBAL de permisos
-- Base de datos: AuthDb
-- Orden de ejecución: 1 de 4
-- Idempotente: seguro de ejecutar múltiples veces
--
-- NOTA: la tabla Permisos es un catálogo GLOBAL (sin TenantId).
--       Los permisos se comparten entre todos los tenants; lo que
--       cambia por tenant es el mapeo RolPermisos.
-- ============================================================

SET NOCOUNT ON;
GO

-- ============================================================
-- PÓRTICOS (catálogo de peajes)
-- ============================================================
INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Ver Pórticos','tracker.porticos.ver','Permite ver el catálogo de pórticos y su geometría','Tracker.Porticos',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.porticos.ver');

INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Listar Pórticos','tracker.porticos.listar','Permite listar pórticos (GET /api/porticos y /geojson)','Tracker.Porticos',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.porticos.listar');

INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Reetiquetar Pórticos','tracker.porticos.reetiquetar','Permite reasignar la autopista/concesión de pórticos (POST /api/porticos/reetiquetar)','Tracker.Porticos',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.porticos.reetiquetar');

INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Administrar Pórticos','tracker.porticos.administrar','Permite crear, editar y eliminar pórticos del catálogo','Tracker.Porticos',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.porticos.administrar');

-- ============================================================
-- TARIFAS Y BANDAS HORARIAS (back-office)
-- ============================================================
INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Ver Tarifas','tracker.tarifas.ver','Permite consultar las tarifas por pórtico, banda y categoría','Tracker.Tarifas',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.tarifas.ver');

INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Cargar Tarifas','tracker.tarifas.cargar','Permite la carga masiva de tarifas (POST /api/tarifas/bulk)','Tracker.Tarifas',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.tarifas.cargar');

INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Eliminar Tarifas','tracker.tarifas.eliminar','Permite eliminar tarifas cargadas','Tracker.Tarifas',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.tarifas.eliminar');

INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Ver Bandas Horarias','tracker.bandas.ver','Permite consultar las ventanas horarias de banda por pórtico','Tracker.Tarifas',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.bandas.ver');

INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Cargar Bandas Horarias','tracker.bandas.cargar','Permite la carga masiva de bandas horarias (POST /api/bandas-horario/bulk)','Tracker.Tarifas',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.bandas.cargar');

INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Eliminar Bandas Horarias','tracker.bandas.eliminar','Permite eliminar bandas horarias (DELETE /api/bandas-horario/{codigo})','Tracker.Tarifas',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.bandas.eliminar');

-- ============================================================
-- TRACKING / GPS
-- ============================================================
INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Emitir Posición GPS','tracker.tracking.emitir','Permite enviar coordenadas GPS al hub (SendCoordinate / ingest)','Tracker.Tracking',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.tracking.emitir');

INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Ver Posición Propia','tracker.tracking.ver','Permite ver la última posición y el recorrido de los dispositivos propios','Tracker.Tracking',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.tracking.ver');

INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Seguir en Vivo','tracker.tracking.vivo','Permite suscribirse al LiveHub y seguir dispositivos en tiempo real','Tracker.Tracking',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.tracking.vivo');

INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Ver Historial de Recorridos','tracker.tracking.historial','Permite consultar el histórico de recorridos (GET /api/devices/{id}/track)','Tracker.Tracking',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.tracking.historial');

INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Ver Tracking de Toda la Flota','tracker.tracking.flota','Permite ver la posición y recorrido de TODOS los dispositivos del tenant, no solo los propios','Tracker.Tracking',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.tracking.flota');

-- ============================================================
-- TRÁNSITOS Y GASTOS
-- ============================================================
INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Ver Tránsitos','tracker.transitos.ver','Permite ver los pasos por pórtico detectados','Tracker.Gastos',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.transitos.ver');

INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Ver Resumen de Gastos','tracker.gastos.resumen','Permite consultar el gasto agregado por día/semana/mes (GET /api/gastos/resumen)','Tracker.Gastos',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.gastos.resumen');

INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Ver Detalle de Gastos','tracker.gastos.detalle','Permite consultar el detalle de cada tránsito tarificado (GET /api/gastos/detalle)','Tracker.Gastos',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.gastos.detalle');

INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Exportar Gastos','tracker.gastos.exportar','Permite exportar los gastos a CSV/Excel','Tracker.Gastos',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.gastos.exportar');

INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Ver Gastos de Toda la Flota','tracker.gastos.flota','Permite ver el gasto consolidado de todos los vehículos del tenant','Tracker.Gastos',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.gastos.flota');

-- ============================================================
-- VEHÍCULOS (Fase 2 del dominio: categoría real por vehículo)
-- ============================================================
INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Ver Vehículos','tracker.vehiculos.ver','Permite ver los vehículos y su dispositivo asociado','Tracker.Vehiculos',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.vehiculos.ver');

INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Listar Vehículos','tracker.vehiculos.listar','Permite listar los vehículos del usuario o del tenant','Tracker.Vehiculos',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.vehiculos.listar');

INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Crear Vehículo','tracker.vehiculos.crear','Permite registrar un vehículo nuevo','Tracker.Vehiculos',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.vehiculos.crear');

INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Actualizar Vehículo','tracker.vehiculos.actualizar','Permite editar los datos y la categoría tarifaria de un vehículo','Tracker.Vehiculos',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.vehiculos.actualizar');

INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Eliminar Vehículo','tracker.vehiculos.eliminar','Permite eliminar un vehículo','Tracker.Vehiculos',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.vehiculos.eliminar');

INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Asignar Conductor a Vehículo','tracker.vehiculos.asignar_conductor','Permite asociar un usuario conductor a un vehículo de la flota','Tracker.Vehiculos',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.vehiculos.asignar_conductor');

-- ============================================================
-- ALERTAS
-- ============================================================
INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Recibir Alertas de Pórtico','tracker.alertas.recibir','Permite recibir la notificación de paso por pórtico con su precio','Tracker.Alertas',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.alertas.recibir');

INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Configurar Alertas','tracker.alertas.configurar','Permite configurar umbrales y preferencias de alertas','Tracker.Alertas',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.alertas.configurar');

-- ============================================================
-- REPORTES
-- ============================================================
INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Ver Reportes de Tracker','tracker.reportes.ver','Permite ver los reportes analíticos de gasto y uso','Tracker.Reportes',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.reportes.ver');

INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Exportar Reportes de Tracker','tracker.reportes.exportar','Permite exportar los reportes de Tracker','Tracker.Reportes',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.reportes.exportar');

-- ============================================================
-- ADMINISTRACIÓN DEL SERVICIO
-- ============================================================
INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Administrar Tracker','tracker.admin.administrar','Acceso total a la configuración del servicio Tracker','Tracker.Admin',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.admin.administrar');

INSERT INTO Permisos (Id,Nombre,Codigo,Descripcion,Categoria,Activo,FechaCreacion)
SELECT NEWID(),'Ejecutar Tareas Internas','tracker.admin.interno','Permite invocar los endpoints internos del Worker (/internal/*)','Tracker.Admin',1,GETUTCDATE()
WHERE NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo='tracker.admin.interno');

-- ============================================================
-- VERIFICACIÓN
-- ============================================================
SELECT Categoria, Codigo, Nombre, Activo
FROM   Permisos
WHERE  Codigo LIKE 'tracker.%'
ORDER  BY Categoria, Codigo;

SELECT COUNT(*) AS TotalPermisosTracker FROM Permisos WHERE Codigo LIKE 'tracker.%';
GO
