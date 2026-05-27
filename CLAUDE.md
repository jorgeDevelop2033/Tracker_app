# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Comandos de build y ejecución

```bash
# Compilar toda la solución
dotnet build src/Tracker.sln

# Ejecutar servicios individuales
dotnet run --project src/Tracker.WebSocket/Tracker.WebSocket.csproj
dotnet run --project src/Tracker.Worker/Tracker.Worker.csproj
dotnet run --project src/Tracker.API/Tracker.API.csproj

# Cliente de prueba (se conecta al VPS en vivo)
dotnet run --project src/Tracker.WebSocketClient/Tracker.WebSocketClient.csproj

# Publicar WebSocket para deploy
dotnet publish src/Tracker.WebSocket/Tracker.WebSocket.csproj -c Release -o ./publish/Tracker.WebSocket

# Migraciones EF Core (ejecutar desde la raíz)
dotnet ef migrations add <NombreMigracion> --project src/Tracker.Infrastructure --startup-project src/Tracker.Worker
dotnet ef database update --project src/Tracker.Infrastructure --startup-project src/Tracker.Worker
```

## Arquitectura

**.NET 9**, Clean Architecture. Dominio de seguimiento GPS en autopistas de peaje (pórticos, tránsitos, vehículos). El dominio **está implementado** con EF Core + SQL Server + NetTopologySuite (geometría espacial SRID 4326).

### Grafo de dependencias

```
Tracker.Domain          (sin dependencias — modelo puro)
Tracker.Contracts       (records/enums compartidos entre proyectos; sin dependencias)
    ↑
Tracker.Infrastructure  (EF Core, repositorios, migraciones)
    ↑
Tracker.Application     (servicios de aplicación, interfaces de repos, DTOs)
    ↑
Tracker.API             (Web API — endpoints de dominio aún no implementados)
Tracker.WebSocket       (SignalR + Kafka producer — único servicio desplegado)
Tracker.Worker          (Kafka consumer + ingest + detección de pórticos)
Tracker.WebSocketClient (cliente consola de prueba)
```

### Flujo de datos en producción

```
Móvil/App  →  SignalR (TrackerHub)  →  Kafka topic "tracker.gps.events"  →  GpsConsumer (Worker)
                                                                                  ↓
                                                                         GpsIngestService   → GpsFix (SQL Server)
                                                                         PorticoDetectionService → Transito (SQL Server)
```

### Componentes activos

**Tracker.WebSocket** — único servicio desplegado. `TrackerHub.SendCoordinate(CoordinateDto)` recibe GPS del móvil, construye un `GpsEvent` de `Tracker.Contracts` y lo publica en Kafka usando `KafkaPublisher` (Protobuf + Schema Registry). Ya **no** rebroadcastea a clientes. CORS abierto (`AllowReactNative`). Serilog a consola + fichero rotativo (`Logs/coordinates-*.log`).

**Tracker.Worker** — Kafka consumer (`GpsConsumer : BackgroundService`). Deserializa mensajes Protobuf (`GpsEventV2`) con Schema Registry. Por cada mensaje crea un scope DI y ejecuta en secuencia:
1. `IGpsIngestService.IngestAsync` → persiste `GpsFix` en SQL Server.
2. `IPorticoDetectionService.DetectarYGuardarAsync` → algoritmo de detección de paso por pórtico (ver abajo). Manual commit de offset tras éxito.

**Tracker.API** — Web API scaffolded; solo tiene el endpoint `WeatherForecast` por defecto. Aún no expone endpoints de dominio.

### Algoritmo de detección de pórtico (`PorticoDetectionService`)

1. Crea un `Point` NTS (lon, lat, SRID 4326) a partir del evento GPS.
2. Consulta `IPorticoRepository.GetNearAsync(punto, radioM=50, take=5)` — candidatos ordenados por distancia en BD.
3. Si el pórtico tiene `Corredor` (LineString) y el evento tiene `HeadingDeg`, calcula el bearing de la línea y descarta si la diferencia angular supera 45°.
4. De-bounce: busca tránsitos recientes del mismo pórtico en ventana ±90 s (`ITransitoRepository.GetByPorticoAsync`). Si existe, descarta.
5. Crea y persiste un `Transito` con el primer match válido y retorna.

### Modelo de dominio implementado

- **Entidades**: `Portico`, `Transito`, `TransitoPortico`, `GpsFix`, `TarifaPortico`
- **Propiedades espaciales** (NTS): `Portico.Ubicacion` (Point), `Portico.Corredor` (LineString), `Transito.Posicion` (Point)
- **Enums** (en `Tracker.Contracts`): `VehicleCategory`, `Banda`
- **Índices espaciales**: aplicar manualmente el script `src/Tracker.Infrastructure/Scripts/CreateSpatialIndexes.sql` en SQL Server

### Convenciones de persistencia (TrackerDbContext)

- Schema por defecto: `tracker`
- Cualquier propiedad `DateTime` cuyo nombre termine en `Utc` se mapea a `datetime2`.
- Entidades que hereden `BaseEntity` obtienen automáticamente `rowversion` como concurrency token.

## Configuración requerida

| Proyecto | Clave | Valor por defecto |
|---|---|---|
| Worker | `ConnectionStrings:TrackerDb` | SQL Server local `localhost,1433` |
| Worker / WebSocket | `Kafka:BootstrapServers` | `45.7.228.18:9092` |
| Worker / WebSocket | `SchemaRegistry:Url` | `http://45.7.228.18:8086` |
| Worker | `Kafka:Topic` | `tracker.gps.events` |

En producción (`appsettings.Production.json` de WebSocket) el broker usa tres puertos: `9092,9094,9095` y `SubjectNameStrategy: TopicRecord`.

## Despliegue

CI/CD en `.github/workflows/dotnet-desktop.yml`. Se activa en push a **`master2`** (no a `master`). Flujo:
1. Build + publish de `Tracker.WebSocket` en `windows-latest`.
2. SSH + rsync al VPS (`VM_HOST`, `VM_PORT`, `VM_USER`, `SSH_PRIVATE_KEY`).
3. `docker build` + `docker run -p 5137:80 -v /var/log/tracker:/app/Logs`.

Endpoint live: `http://45.7.228.18:5137/trackerHub`.
