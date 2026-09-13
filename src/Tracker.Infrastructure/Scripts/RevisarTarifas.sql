/* =====================================================================
   Revisión de tarifas — Tracker
   Base: TrackerDb (esquema `tracker`). Una sola BD: API y Worker comparten
   devsogu-sqlserver,1433.

   "Vigente" = VigenteDesde <= ahora AND (VigenteHasta IS NULL OR > ahora).
   Una tarifa cerrada deja al pórtico cobrando 0, así que sólo cuenta lo abierto.
   ===================================================================== */

DECLARE @ahora datetime2 = SYSUTCDATETIME();

/* 1) Cobertura por autopista: cuántos pórticos tienen precio y cuántos no. */
SELECT  p.Autopista,
        COUNT(*)                                   AS Porticos,
        SUM(CASE WHEN v.n > 0 THEN 1 ELSE 0 END)   AS ConTarifa,
        SUM(CASE WHEN v.n = 0 THEN 1 ELSE 0 END)   AS SinTarifa
FROM        tracker.Porticos p
CROSS APPLY (SELECT COUNT(*) AS n
             FROM   tracker.TarifasPortico t
             WHERE  t.PorticoId = p.Id
               AND  t.VigenteDesde <= @ahora
               AND (t.VigenteHasta IS NULL OR t.VigenteHasta > @ahora)) v
GROUP BY p.Autopista
ORDER BY SinTarifa DESC, p.Autopista;


/* 2) Autopista Central en detalle: qué bandas y categorías quedaron vigentes.
      Tras la carga 2026 se esperan 9 filas por pórtico (C1/C2/C3 x las bandas
      que el PDF publica para ese pórtico). */
SELECT  p.Codigo,
        t.Categoria,                       -- 1=C1 2=C2 3=C3
        t.Banda,                           -- 0=TBFP 1=TBP 2=TS
        t.ValorFijo,
        t.ValorPorKm,
        t.LongitudKmSnapshot               AS Km,
        CONVERT(varchar(19), t.VigenteDesde, 120) AS Desde
FROM   tracker.TarifasPortico t
JOIN   tracker.Porticos p ON p.Id = t.PorticoId
WHERE  p.Autopista = 'Autopista Central'
  AND  t.VigenteDesde <= @ahora
  AND (t.VigenteHasta IS NULL OR t.VigenteHasta > @ahora)
ORDER BY p.Codigo, t.Categoria, t.Banda;


/* 3) Las 31 residuales: tarifas C1 en $/km que sobrevivieron a la recarga.
      Son bandas que el PDF 2026 ya no publica para ese pórtico. Si esta
      consulta devuelve 0 filas, el cierre se aplicó bien. */
SELECT  p.Codigo, t.Categoria, t.Banda, t.ValorPorKm, t.ValorFijo,
        CONVERT(varchar(19), t.VigenteDesde, 120) AS Desde
FROM   tracker.TarifasPortico t
JOIN   tracker.Porticos p ON p.Id = t.PorticoId
WHERE  p.Autopista = 'Autopista Central'
  AND  t.ValorPorKm IS NOT NULL          -- la carga nueva usa ValorFijo
  AND  t.VigenteDesde <= @ahora
  AND (t.VigenteHasta IS NULL OR t.VigenteHasta > @ahora)
ORDER BY p.Codigo, t.Banda;


/* 4) Duplicados: la misma combinación (pórtico, categoría, banda) abierta más
      de una vez. Debe devolver 0 filas; si no, hay doble cobro potencial. */
SELECT  p.Codigo, t.Categoria, t.Banda, COUNT(*) AS Vigentes
FROM   tracker.TarifasPortico t
JOIN   tracker.Porticos p ON p.Id = t.PorticoId
WHERE  t.VigenteDesde <= @ahora
  AND (t.VigenteHasta IS NULL OR t.VigenteHasta > @ahora)
GROUP BY p.Codigo, t.Categoria, t.Banda
HAVING COUNT(*) > 1
ORDER BY Vigentes DESC, p.Codigo;


/* 5) Pórticos sin ninguna tarifa vigente: el trabajo de carga pendiente. */
SELECT  p.Autopista, p.Codigo, p.Sentido, p.Descripcion
FROM    tracker.Porticos p
WHERE   NOT EXISTS (SELECT 1
                    FROM   tracker.TarifasPortico t
                    WHERE  t.PorticoId = p.Id
                      AND  t.VigenteDesde <= @ahora
                      AND (t.VigenteHasta IS NULL OR t.VigenteHasta > @ahora))
ORDER BY p.Autopista, p.Codigo;
