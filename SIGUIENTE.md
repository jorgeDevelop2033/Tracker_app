# Qué sigue — tarifas

Estado al 2026-09-13. Ver [BITACORA.md](BITACORA.md) para lo ya hecho.

## El criterio: priorizar por uso real, no por pórticos faltantes

Quedan 132 pórticos sin tarifa, pero **no son 132 problemas equivalentes**.
La mayoría es catálogo muerto. Esto es lo que se usa de verdad:

| Autopista | Tránsitos | Sin cobro |
|---|---:|---:|
| **Autopista Central** | 101 | 15 |
| Ruta del Maipo | 22 | 11 |
| Survias (Talca-Chillán) | 8 | 3 |
| Costanera Norte | 5 | 4 |
| Ruta 5 (interurbano) | 3 | 2 |

Autopista Central es el **73%** de los tránsitos. Vespucio Norte, Vespucio Sur,
Autopista del Sol y Autopistas Urbanas Santiago — 51 pórticos entre las cuatro —
**no registran ni un tránsito**.

---

## Paso 1 · Los 10 pórticos que sí están fallando

Tienen tránsitos reales cobrando **$0**. Es el único trabajo que corrige dinero
mal calculado hoy.

| Pórtico | Autopista | Pasos sin cobro |
|---|---|---:|
| T1 | Autopista Central | 4 |
| P2.2 | Costanera Norte | 3 |
| OSM13743054369 (Troncal Río Maipo) | Ruta 5 | 3 |
| P2.1 | Autopista Central | 3 |
| P3.1 / P3.2 / P3.3 / P3.4 | Autopista Central | 2 c/u |
| P6.2 | Costanera Norte | 1 |
| P210 | Ruta 5 | 1 |

Dos cosas a resolver dentro de este paso:

- `OSM13743054369` es un **tercer pórtico de Río Maipo** — se cargó el `...399`,
  este es otro. Los pares de sentido siguen incompletos.
- `P2.1` y `P3.1-P3.4` **no están en el PDF de Autopase**, así que están mal
  etiquetados como Autopista Central. Hay que ver a qué concesión pertenecen.

## Paso 2 · Horarios de banda de Costanera Norte

CN tiene tarifas pero **no horarios**, así que todo cobra TBFP. En hora punta eso
subestima 2× o 3×. Hay 5 tránsitos reales, sí se usa.

## Paso 3 · Mergear el PR #4 *(5 min)*

https://github.com/jorgeDevelop2033/Tracker_app/pull/4 — `POST /api/tarifas/cerrar`,
ya probado y compilando. Sin él, cada recarga de tarifario vuelve a dejar tarifas
residuales cobrando en silencio (lo que hoy hubo que arreglar a mano por SQL).

## Paso 4 · Plazas de Ruta 5 que no existen en el catálogo

Tienen tarifa publicada pero no hay pórtico: Pua, Quepe, Lanco, La Unión,
Cuatro Vientos, Bypass Pto Montt, Pargua (Sur) y Las Vegas, El Melón, Pichidangui,
Troncal Norte/Sur, Punta Colorada, Cachiyuyo, Totoral (Norte).
Insertar geolocalizando por km, como se hizo con Río Claro.
Sólo importa para viajes largos.

---

## Lo que NO conviene hacer todavía

**Vespucio Norte/Sur, Autopista del Sol, Autopistas Urbanas Santiago.**
51 pórticos, cero tránsitos, y para Vespucio Sur el mapeo con el PDF ya se sabe
que no cuadra. Máximo costo, mínimo retorno.

## Aparte (no es de tarifas)

La VPS está con **load average 7,08 y sólo 2 cores** — saturada. Si el Worker se
degrada, se pierden tránsitos.
