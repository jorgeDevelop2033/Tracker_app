# Provisión de Tracker en el servicio Auth

Scripts para dar de alta el aplicativo **Tracker** dentro del servicio de
autenticación del ERP (`AuthDb`): tenant, catálogo de permisos, roles por plan
y usuarios administradores.

Todos los scripts son **idempotentes**: se pueden ejecutar varias veces sin
duplicar datos. Verificado ejecutándolos dos veces seguidas sobre una base
limpia (los conteos no cambian).

---

## Cómo se modela el "plan contratado"

Conviene decirlo explícito porque condiciona todo lo demás:

> **El servicio Auth no tiene entidad de Planes.** `Tenants.Plan` es una
> columna `nvarchar(60)` de **texto libre**, sin validación ni lógica asociada
> en el código. La tabla `FeatureFlagsTenant` existe en el modelo pero **no la
> consume ninguna parte del código**.

Por lo tanto el plan se materializa como **un rol con su set de permisos**:

| Plan comercial | Rol que se asigna | Dónde vive |
|---|---|---|
| Free | `TrackerFree` | tenant GLOBAL |
| Pro | `TrackerPro` | tenant GLOBAL |
| Flota (B2B) | `TrackerFlotaAdmin` / `TrackerFlotaSupervisor` / `TrackerFlotaConductor` | tenant de la empresa |

**Cambiar de plan = cambiar el rol asignado en `UsuarioRoles`.** El campo
`Tenants.Plan` se mantiene como etiqueta informativa/comercial (para reportes o
facturación), pero no otorga ni quita capacidades por sí solo.

### Matriz de capacidades por plan

|                          | Free | Pro | Flota |
|--------------------------|:----:|:---:|:-----:|
| Ver pórticos en el mapa  | ✅ | ✅ | ✅ |
| Emitir GPS / tracking    | ✅ | ✅ | ✅ |
| Alerta de paso + precio  | ✅ | ✅ | ✅ |
| Gasto resumido           | ✅ | ✅ | ✅ |
| Gasto detallado          | ❌ | ✅ | ✅ |
| Historial de recorridos  | ❌ | ✅ | ✅ |
| Exportar gastos/reportes | ❌ | ✅ | ✅ |
| Multi-vehículo (CRUD)    | ❌ | ✅ | ✅ |
| Seguir en vivo           | ❌ | ✅ | ✅ |
| Ver **toda** la flota    | ❌ | ❌ | ✅ |
| Asignar conductores      | ❌ | ❌ | ✅ |

---

## Modelo de tenancy (híbrido)

- **Tenant GLOBAL (B2C)** — conductores que se registran solos en la app mobile.
  Su `Id` es **fijo**: `11111111-1111-1111-1111-111111111111`. No es arbitrario:
  está hardcodeado en `Auth.Domain/Tenancy/TenancyDefaults.cs` y lo usa el query
  filter de `AuthDbContext`. **No cambiarlo.**
- **Tenant B2B** — una empresa de flota que contrata. Uno por cliente, con su
  propio `Codigo` y su propio `Plan`.

---

## Orden de ejecución

| # | Script | Qué hace |
|---|--------|----------|
| 1 | `01_tracker_permisos.sql` | Inserta los 32 permisos `tracker.*` en el catálogo **global** |
| 2 | `02_tracker_tenant.sql` | Crea el tenant GLOBAL y el tenant B2B, con sus sucursales |
| 3 | `03_tracker_roles.sql` | Crea los 8 roles y sincroniza su mapeo de permisos |
| 4 | `04_tracker_admin.sql` | Crea los usuarios administradores y les asigna rol |

`Permisos` es un **catálogo global sin `TenantId`**: los permisos se comparten
entre todos los tenants. Lo que cambia por tenant es el mapeo en `RolPermisos`.

---

## Parámetros a ajustar por ambiente

Antes de ejecutar, editar las variables del bloque `PARÁMETROS` en cada script.
El código del tenant B2B debe ser **el mismo** en los scripts 02, 03 y 04.

| Variable | Script | Valor por defecto |
|---|---|---|
| `@B2BNombre` | 02 | `Tracker Demo Flota` |
| `@B2BCodigo` | 02, 03, 04 | `TRACKER-DEMO` |
| `@B2BPlan` | 02 | `flota` |
| `@AdminEmail` | 04 | `admin@tracker.devsogu.cl` |
| `@FlotaEmail` | 04 | `flota@tracker.devsogu.cl` |

---

## Contraseña del administrador

El hash **no se puede generar en T-SQL**. El servicio usa PBKDF2-HMAC-SHA256,
210.000 iteraciones, salt de 16 bytes y un **pepper** secreto que vive fuera del
repositorio (variable de entorno `PasswordHashing__Pepper`).

```powershell
# El pepper DEBE ser el del ambiente destino
$hash = .\hash-password.ps1 -Password 'ClaveSegura' -Pepper $env:PasswordHashing__Pepper
```

Pegar el resultado en `@AdminPasswordHash` / `@FlotaPasswordHash` del script 04.

> ⚠️ **Un pepper distinto produce un hash que no valida al iniciar sesión.**
> Verificado en pruebas: el mismo password con pepper distinto da `False` en
> `VerifyPassword`. Confirme el pepper de UAT y de PROD por separado.

Si deja el hash en `NULL`, el usuario se crea en estado **Inactivo (4)** y sin
clave utilizable: deberá completarse por el flujo de recuperación de contraseña.
Esta es la opción **recomendada para PROD** — evita que una clave viaje en un
script versionado.

---

## Ejecución

```powershell
$env:SQLCMDPASSWORD = '<password>'
$d = 'db/auth'

foreach ($f in '01_tracker_permisos.sql','02_tracker_tenant.sql','03_tracker_roles.sql','04_tracker_admin.sql') {
    sqlcmd -S <servidor> -U <usuario> -C -d AuthDb -i "$d/$f" -b
    if ($LASTEXITCODE -ne 0) { Write-Error "Fallo en $f"; break }
}
```

El flag `-b` aborta ante el primer error; sin él, un fallo intermedio pasa
desapercibido y los scripts siguientes corren sobre un estado incompleto.

### Ejecución contra el VPS

`deploy-vps.sh` hace el ciclo completo contra `devsogu-sqlserver`: copia los
scripts, **respalda `AuthDb`** y ejecuta los cuatro en orden abortando al primer
error.

```bash
./db/auth/deploy-vps.sh
```

No pide la clave de SQL: se autentica con la variable `MSSQL_SA_PASSWORD` que el
propio contenedor ya expone, de modo que la credencial nunca sale del VPS. Los
valores por defecto (host, puerto, contenedor, base) se sobrescriben por
`VPS_HOST`, `VPS_PORT`, `SQL_CONTAINER`, `SQL_DB`, `SQL_USER`. Requiere acceso
SSH al VPS.

> **`SET QUOTED_IDENTIFIER ON` no es opcional.** `sqlcmd` lo deja apagado por
> defecto y `Tenants` tiene un índice filtrado: sin la directiva, el `INSERT`
> del script 02 falla con *Msg 1934 — INSERT failed because the following SET
> options have incorrect settings*. Los cuatro scripts la fijan en su cabecera.
> `sqlcmd` vive en `/opt/mssql-tools18/bin/` dentro del contenedor.

---

## Promoción a UAT y PROD

1. **Respaldo** de `AuthDb` antes de ejecutar en cada ambiente.
2. Ajustar los parámetros (`@B2BCodigo`, correos) al ambiente.
3. Generar el hash **con el pepper de ese ambiente** (o dejar `NULL` en PROD).
4. Ejecutar 01 → 02 → 03 → 04 con `-b`.
5. Revisar el `SELECT` de verificación al final de cada script.
6. Validar el login del admin y que el JWT emitido traiga los permisos `tracker.*`.

Los scripts 01 y 03 se re-ejecutan sin riesgo cuando se agreguen permisos
nuevos: el 03 **sincroniza** (agrega los que faltan y quita los sobrantes),
limitando el borrado a permisos `tracker.*` de los roles que él mismo gestiona,
para no pisar asignaciones manuales de otros módulos.

---

## Resultado esperado

Tras ejecutar los 4 scripts sobre una base limpia:

| Tenant | Rol | Permisos |
|---|---|---:|
| GLOBAL | TrackerAdmin | 32 |
| GLOBAL | TrackerFree | 9 |
| GLOBAL | TrackerPro | 21 |
| GLOBAL | TrackerSoporte | 12 |
| TRACKER-DEMO | TrackerAdmin | 32 |
| TRACKER-DEMO | TrackerFlotaAdmin | 24 |
| TRACKER-DEMO | TrackerFlotaConductor | 11 |
| TRACKER-DEMO | TrackerFlotaSupervisor | 17 |

Totales: 32 permisos `tracker.*`, 2 tenants, 8 roles, 158 filas en `RolPermisos`.

---

## Pendiente del lado del código (no cubierto por estos scripts)

Los scripts dejan la autorización lista en la base, pero `Tracker.API` todavía
**no valida permisos**: sus endpoints no tienen `RequireAuthorization` (salvo la
cabecera `X-Internal-Key` en los de carga). Para que estos roles surtan efecto
hay que:

1. Añadir autenticación JWT en `Tracker.API` apuntando al emisor del Auth.
2. Marcar cada endpoint con su permiso, siguiendo el patrón del ERP:
   ```csharp
   app.MapGet("/api/gastos/detalle", ...).RequireAuthorization("tracker.gastos.detalle");
   ```
3. Filtrar por `DeviceId`/vehículo del usuario salvo que tenga
   `tracker.tracking.flota` o `tracker.gastos.flota`.

Sin el paso 1 y 2, los permisos existen pero nadie los exige.
