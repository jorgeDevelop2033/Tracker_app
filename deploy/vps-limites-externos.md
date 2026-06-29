# Límites de memoria — contenedores FUERA de este repo (stack devsogu/ERP)

> VPS de 8 GB compartida. El stack Tracker ya tiene límites en sus `docker-compose.yml`.
> Aquí van los que **no** están en este repo: SQL Server (el `Exited 137` = OOM) y las ~12 apps ERP.

## Presupuesto de RAM objetivo (8 GB, dejar ~1 GB al host)

| Grupo | RAM | Cómo se aplica |
|---|---|---|
| **SQL Server** (`devsogu-sqlserver`) | 2 GB | `mem_limit` **+** `MSSQL_MEMORY_LIMIT_MB` (interno) |
| Kafka | 1 GB | ya en repo |
| Schema Registry | 0.5 GB | ya en repo |
| Tracker api/web/ws/worker | ~1.3 GB | ya en repo |
| ~12 apps ERP .NET | ~3 GB | 256 MB c/u |
| Redis / nginx / alloy / cadvisor | ~0.5 GB | límites pequeños |

## 1) SQL Server — el más crítico (causa los `Exited 137`)

SQL Server por defecto intenta usar **toda la RAM disponible**. Hay que limitarlo por DENTRO y por FUERA.

En el `docker-compose.yml` del stack devsogu (donde esté definido `devsogu-sqlserver`):

```yaml
  devsogu-sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_MEMORY_LIMIT_MB: "1800"   # tope INTERNO del buffer pool (clave)
    mem_limit: 2g                     # tope del contenedor
    mem_reservation: 1g
    cpus: 1.5
```

Si no puedes editar ese compose ahora mismo, parche en caliente del tope interno sin reiniciar:

```bash
docker exec -i devsogu-sqlserver /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P "$SA_PASSWORD" -Q \
  "EXEC sp_configure 'show advanced options',1; RECONFIGURE;
   EXEC sp_configure 'max server memory (MB)',1800; RECONFIGURE;"
```
(El `mem_limit` del contenedor sí requiere recrearlo con `docker compose up -d`.)

## 2) Apps ERP .NET (~12 contenedores)

Edita el/los compose del stack devsogu y añade a cada servicio .NET:

```yaml
    mem_limit: 256m
    mem_reservation: 96m
    cpus: 0.4
    environment:
      DOTNET_gcServer: "0"   # Workstation GC: mucho menos RAM en contenedores chicos
```

## 3) Infra de apoyo

```yaml
  devsogu-redis:    { mem_limit: 256m, cpus: 0.25 }   # + 'redis-server --maxmemory 192mb --maxmemory-policy allkeys-lru'
  devsogu-nginx:    { mem_limit: 128m, cpus: 0.25 }
  grafana/alloy:    { mem_limit: 256m, cpus: 0.25 }
  cadvisor:         { mem_limit: 256m, cpus: 0.25 }   # cAdvisor consume CPU notable; valora apagarlo si no lo usas
```

## 4) Reducir swapping del host (opcional pero recomendado)

Si `free -h` muestra swap en uso constante, el disco frena todo:

```bash
sudo sysctl vm.swappiness=10
echo 'vm.swappiness=10' | sudo tee -a /etc/sysctl.conf
```

## Aplicar y verificar

```bash
docker compose up -d        # en cada carpeta de stack que hayas editado
docker stats --no-stream    # ningún contenedor debería seguir SIN LÍMITE ni cerca de su tope
free -h                     # swap usado debería bajar y mantenerse estable
```
