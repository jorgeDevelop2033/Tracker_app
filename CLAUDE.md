# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Comandos de build y ejecución

```bash
# Compilar toda la solución
dotnet build src/Tracker.sln

# Ejecutar servicios individuales
dotnet run --project src/Tracker.WebSocket/Tracker.WebSocket.csproj   # ingesta móvil → Kafka
dotnet run --project src/Tracker.Worker/Tracker.Worker.csproj         # consumer + detección + seed
dotnet run --project src/Tracker.API/Tracker.API.csproj               # REST + SignalR para el dashboard

# Cliente de prueba (se conecta al VPS en vivo)
dotnet run --project src/Tracker.WebSocketClient/Tracker.WebSocketClient.csproj

# Migraciones EF Core (ejecutar desde la raíz; startup-project puede ser Worker o API)
dotnet ef migrations add <NombreMigracion> --project src/Tracker.Infrastructure --startup-project src/Tracker.Worker
dotnet ef database update --project src/Tracker.Infrastructure --startup-project src/Tracker.Worker

# Imágenes Docker (mismos Dockerfile que usa CI)
docker build -f Dockerfile.api       -t tracker-api .
docker build -f Dockerfile.websocket -t tracker-websocket .
docker build -f Dockerfile.worker    -t tracker-worker .
```

No hay proyecto de tests en la solución.

## Arquitectura

**.NET 9**, Clean Architecture. Dominio de seguimiento GPS en autopistas de peaje chilenas (pórticos, tránsitos, tarifas por banda horaria). Dominio implementado con EF Core + SQL Server + NetTopologySuite (geometría espacial SRID 4326).

### Grafo de dependencias

```
Tracker.Domain          (sin dependencias — entidades, interfaces de repos, abstracciones)
Tracker.Contracts       (records/enums compartidos: VehicleCategory, Banda, DiaTipo)
    ↑
Tracker.Application     (servicios, DTOs, PorticoDetectionService, CalendarioChile/FestivosChile)
    ↑
Tracker.Infrastructure  (EF Core, repositorios, migraciones, seed, AddInfrastructure)
    ↑
Tracker.API             (REST de lectura + carga admin + SignalR LiveHub)
Tracker.WebSocket       (SignalR TrackerHub + Kafka producer — ingesta del móvil)
Tracker.Worker          (Kafka consumer + ingest + detección + tarificación + broadcast)
Tracker.WebSocketClient (cliente consola de prueba)
```

> Nota: `PorticoDetectionService` vive físicamente en `Tracker.Application/Services` pero declara el namespace `Tracker.Worker.Infrastructure.Services` (histórico). No te confíes del namespace para localizarlo.

### Flujo de datos en producción

```
Móvil  →  SignalR TrackerHub (WebSocket)  →  Kafka "tracker.gps.events"  →  GpsConsumer (Worker)
                                                                                 │
                        ┌────────────────────────────────────────────────────────┤
                        ▼                          ▼                              ▼
              GpsIngestService            PorticoDetectionService        ILiveBroadcaster (HTTP)
              → GpsFix (SQL)              → Transito (SQL, con precio)    → POST Tracker.API /internal/*
                                                                                 │
                                                                          LiveHub (SignalR)
                                                                                 ▼
                                                                          Dashboard Angular
```

El Worker es el orquestador: por cada mensaje Kafka persiste el fix, corre la detección/tarificación, y **empuja por HTTP** la posición (y el tránsito si lo hubo) a `Tracker.API`, que lo reemite por SignalR al dashboard. El broadcast es **best-effort**: cualquier fallo HTTP se traga (el dato ya está persistido) para no romper el consumo de Kafka.

### Componentes

**Tracker.WebSocket** — ingesta. `TrackerHub.SendCoordinate(CoordinateDto)` recibe GPS, construye un `GpsEvent` de `Tracker.Contracts` y lo publica en Kafka con `KafkaPublisher` (Protobuf + Schema Registry). No rebroadcastea. CORS abierto (`AllowReactNative`). Serilog a consola + fichero (`Logs/coordinates-*.log`). Endpoint live: `https://tracker.devsogu.cl/trackerHub` (directo a la VPS: `http://45.7.229.192:5137/trackerHub`).

**Tracker.Worker** — `GpsConsumer : BackgroundService`. Deserializa Protobuf `GpsEventV2` con Schema Registry, `EnableAutoCommit=false` con commit manual tras éxito. Por mensaje abre un scope DI y ejecuta en secuencia: `IGpsIngestService.IngestAsync` → `IPorticoDetectionService.DetectarYGuardarAsync` → `ILiveBroadcaster.BroadcastAsync` (+ `BroadcastTransitoAsync` si hubo tránsito). **Al arranque** ejecuta `PorticoSeeder.SeedAsync` (catálogo OSM).

**Tracker.API** — REST de lectura para el dashboard (`/api/porticos`, `/api/porticos/geojson`, `/api/devices/{id}/last|track`, `/api/gastos/resumen|detalle`), endpoints internos que recibe del Worker (`/internal/live`, `/internal/transito`), carga admin de tarifas/bandas (`/api/tarifas/bulk`, `/api/bandas-horario/bulk`, etc.) y el `LiveHub` SignalR en `/liveHub`. Migra la BD al arranque con `MigrateAsync()`.

### Detección de pórtico y tarificación (`PorticoDetectionService`)

1. Crea `Point` NTS (lon, lat, SRID 4326) del evento.
2. `IPorticoRepository.GetNearAsync(punto, radioM=50, take=5)` — candidatos ordenados por distancia en BD.
3. Si el pórtico tiene `Corredor` (LineString) y el evento trae `HeadingDeg`, descarta si la diferencia de bearing supera **45°**.
4. De-bounce: descarta si hay un tránsito del mismo pórtico en ventana **±90 s** (`ITransitoRepository.GetByPorticoAsync`).
5. **Resolución de precio** del primer candidato válido:
   - `ICalendarioChile.DiaTipoDe(utc)` → `DiaTipo` (Laboral / Sábado / DomingoFestivo), usando hora local `America/Santiago` y `FestivosChile`.
   - `IBandaHorarioRepository.ResolverBandaAsync(porticoId, diaTipo, horaLocal)` → `Banda`.
   - `ITarifaPorticoRepository.GetVigenteAsync(...)` → tarifa vigente; `CalcularPrecio` usa `ValorFijo`, o `ValorPorKm × Km` (snapshot de la tarifa o `Portico.LongitudKm`). Sin tarifa → precio 0 (el tránsito se registra igual).
   - Categoría: la del `Vehiculo` resuelto por la asignación vigente del device; `C1` solo como fallback si el device no está asignado.
6. Persiste el `Transito` (device, vehículo, viaje, fecha/hora local, banda, categoría, tarifa aplicada y snapshots del pórtico) y devuelve un `TransitoDetectadoDto` para el broadcast en vivo.

### Vehículos y viajes

- **`Vehiculo`** (patente única normalizada, `Categoria`) es el sujeto real del cobro. **`AsignacionDispositivo`** liga device↔vehículo **con vigencia** (`DesdeUtc`/`HastaUtc`): la atribución se resuelve siempre con *"¿de quién era este device en el instante del tránsito?"*, nunca "¿de quién es hoy". Índice único filtrado garantiza una sola asignación abierta por device.
- **`Viaje`** lo abre/cierra el conductor (`POST /api/viajes/iniciar` · `/{id}/finalizar`). `CierreViajesService` (Worker) cierra por inactividad los que quedaron colgados (`Viajes:InactividadMinutos`, default 30). Lleva totales desnormalizados y, al cerrar, guarda **`RutaSimplificada`** (Douglas-Peucker ~10 m): es lo que permite purgar `gps_fix` sin perder el recorrido histórico.
- Los `GpsFix` emitidos durante un viaje quedan con `ViajeId`.

### Fecha local y snapshots (claves para la conciliación)

- `Transito.FechaLocal`/`HoraLocal`/`DiaTipo` se **persisten al detectar**, con hora `America/Santiago`. Agrupar por `Utc` mandaba los peajes posteriores a las ~20:00 al día siguiente. Los reportes agrupan por `FechaLocal` (columna indexada, agregación en SQL).
- `Transito` congela `AutopistaSnapshot`/`PorticoCodigoSnapshot`/`SentidoSnapshot` y el `TarifaPorticoId` aplicado. Sin esto, reetiquetar pórticos (como hizo `e5490e4` con 135 de golpe) reescribe reportes ya emitidos, y no hay forma de auditar de dónde salió un monto viejo.
- Índice único `(DeviceId, PorticoId, Utc)`: idempotencia dura ante reentregas de Kafka, por encima del de-bounce heurístico de ±90 s.

### Modelo de dominio

- **Entidades**: `Portico` (con `OsmId`, `Ubicacion` Point, `Corredor` LineString, `LongitudKm`), `Transito`, `Vehiculo`, `AsignacionDispositivo`, `Viaje`, `GpsFix`, `TarifaPortico`, `BandaHorario`.
- **Enums** (`Tracker.Contracts`): `VehicleCategory`, `Banda`, `DiaTipo`, `EstadoViaje`, `EstadoConciliacion`.
- **Seed**: `Seed/porticos_seed.json` (catálogo OSM) es **EmbeddedResource**; upsert idempotente por `OsmId` en cada arranque del Worker.
- **Índices espaciales**: aplicar manualmente `src/Tracker.Infrastructure/Scripts/CreateSpatialIndexes.sql`.

### Convenciones de persistencia (TrackerDbContext)

- Schema por defecto: `tracker`.
- Propiedades `DateTime` cuyo nombre termina en `Utc` → `datetime2`.
- Entidades que heredan `BaseEntity` → `rowversion` como concurrency token automático.

> ⚠️ **Inconsistencia de esquema**: el **Worker** crea la BD con `EnsureCreatedAsync()` (ignora migraciones), mientras la **API** usa `MigrateAsync()`. Si arrancas primero el Worker contra una BD nueva, la tabla `__EFMigrationsHistory` quedará vacía y las migraciones de la API fallarán o divergirán. Para desarrollo aplica migraciones explícitamente con `dotnet ef database update` antes de correr los servicios.

## Configuración requerida

Claves por `appsettings` o variables de entorno (formato Docker: `Seccion__Clave`).

| Proyecto | Clave | Valor por defecto |
|---|---|---|
| Worker / API | `ConnectionStrings:TrackerDb` | SQL Server local `localhost,1433` |
| Worker / WebSocket | `Kafka:BootstrapServers` | dev `localhost:9092` · prod `tracker-kafka:9092` |
| Worker / WebSocket | `SchemaRegistry:Url` | dev `http://localhost:8081` · prod `http://tracker-schema-registry:8081` |
| Worker | `Kafka:Topic` / `Kafka:GroupId` | `tracker.gps.events` / `tracker.worker.gps` |
| Worker | `LiveApi:BaseUrl` | URL de Tracker.API para el broadcast |
| Worker / API | `InternalApi:Key` | clave compartida `X-Internal-Key` (endpoints `/internal/*` y `/api/*/bulk`; **fail-closed**, sin key → 401) |
| API | `Cors:AllowedOrigins` | coma-separados; default `http://localhost:4200` |

⚠️ **Kafka y Schema Registry no están expuestos al exterior**: sus composes (`deploy/tracker-kafka/`, `deploy/tracker-schema-registry/`) no publican `ports:` y el broker se anuncia como `KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://tracker-kafka:9092`. Solo son alcanzables **por hostname dentro de la red `devsogu-net`** — ninguna IP funciona desde fuera, así que **no se puede correr el Worker/WebSocket local contra la Kafka de producción**. Para desarrollo levanta un Kafka + Schema Registry locales (los defaults apuntan a `localhost`).

En producción (`appsettings.Production.json` del WebSocket) los valores ya vienen con hostnames de contenedor y `SubjectNameStrategy: TopicRecord`; los `.env` de `deploy/tracker-*` los sobreescriben.

## Despliegue

Dos workflows conviven en `.github/workflows/`:

- **`build-and-push.yml`** (actual): push a **`master`** o tag `v*` → build de las 3 imágenes (`Dockerfile.api`, `Dockerfile.websocket`, `Dockerfile.worker`) y push a **GHCR** (`ghcr.io/jorgedevelop2033/tracker-*`).
- **`dotnet-desktop.yml`** (legacy): push a `master2` → publish + rsync/SSH al VPS + `docker run` del WebSocket.

Los `docker-compose.yml` por servicio viven en `deploy/tracker-*` (kafka, schema-registry, api, websocket, worker, web) con `.env.example`. Usan la red externa `devsogu-net` e imágenes de GHCR. Ver `deploy/vps-limites-externos.md` y `deploy/vps-perf-diagnostico.sh` para el tuning del VPS (límites de memoria/CPU por contenedor).
