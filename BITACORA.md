# Bitácora

## 2026-09-13 — Tarifas: Autopista Central completa y Ruta 5 iniciada

### Qué se hizo

**Autopista Central: 96 → 195 tarifas.**
Cargadas desde el PDF de [Autopase](https://www.autopase.cl/resources/descargas/01-01-2026.pdf)
(mejor fuente que el MOP: trae valores absolutos por pórtico, categoría y banda,
no sólo $/km). 32 pórticos × C1/C2/C3 × las bandas que publica cada uno.
Antes sólo había C1: **buses y camiones pasaban sin tarifa**.

Se cargaron como `ValorFijo` en vez de `ValorPorKm`, para que el monto no dependa
de que `LongitudKmSnapshot` esté bien.

**31 tarifas que cobraban en silencio: cerradas.**
Al recargar el tarifario, las combinaciones (pórtico, categoría, banda) que la
lámina nueva ya no publica quedan vigentes con el valor viejo — `UpsertVigencia`
sólo cierra lo que sustituye. Eran 31 tarifas C1 en $/km. Se cerraron con
`VigenteHasta` (no se borraron), respaldo en `tracker.TarifasPortico_bak_20260913`,
verificado que 0 de los 139 tránsitos las usaban.

**Ruta 5: +14 tarifas en 8 plazas.**
Río Maipo $1.400 · Angostura $3.800 · Quinta $3.800 · Río Claro $3.300 ·
Retiro $3.300 · Santa Clara $3.400 · Las Maicas $3.400 · Lampa $900.
De paso se corrigieron **Río Claro y Retiro, que estaban a $3.100**.

**Cobertura: 141 → 132 pórticos sin tarifa** (de 188).

### Hallazgos

- **Dos reglas del tarifario que no estaban documentadas**: el **IE +8,8%** sobre
  PA1 y PA2 (D.S. N°380), y que los factores por categoría **difieren por eje** —
  Norte-Sur usa cat2=2× cat3=3×, pero Gral. Velásquez (PA19-29) usa 1,5× y 2×.
  Con ambas, los 195 valores del PDF cuadran exactos contra $/km × km × factor.

- **Trampa de `UpsertVigenciaAsync`**: sólo cierra la tarifa anterior si
  `nueva.VigenteDesde > vigente.VigenteDesde`. Recargar con la misma fecha deja
  **dos vigentes** por combinación. Hay que cargar siempre con fecha posterior.

- **Método para mapear pórticos OSM sin nombre**: cruzar la **latitud** de cada
  pórtico contra el **km publicado** de cada plaza (Ruta 5 tiene kilometraje
  lineal). Así aparecieron plazas ocultas tras códigos `OSM...` y los dos
  sentidos de cada una.

- **La API no arrancaba** (bug preexistente en master): `AddInfrastructure`
  registra `IPorticoDetectionService`, que exige `IUltimaPosicionCache`, pero sólo
  el Worker lo registraba. La validación del DI tumbaba el arranque. Corregido.

- **SSH del VPS es el puerto 21483**, no el 22.

### Cambios en el código

| Commit / PR | Qué |
|---|---|
| `29da7d1` | `GET /api/tarifas/cobertura` + fix del DI que tumbaba la API |
| `RevisarTarifas.sql` | 5 consultas de auditoría en `src/Tracker.Infrastructure/Scripts/` |
| **PR #4** (sin mergear) | `POST /api/tarifas/cerrar` — cierra tarifas sin reemplazo |

### Verificado en producción

- Autopista Central: **195 tarifas vigentes exactas**
- Residuales en $/km: **0**
- Duplicados (pórtico, categoría, banda): **0**
